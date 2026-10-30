using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Calendar;

namespace SPWPOS.Infrastructure.Calendar;

public sealed class CalendarProviderException(int status, string reason, string stage = "calendar") : Exception("Google Calendar request failed.")
{
    public int Status { get; } = status;
    public string Reason { get; } = reason;
    public string Stage { get; } = stage;
    public bool Reconnect => Status == 401 || Reason == "invalid_grant";
}

public sealed class GoogleCalendarGateway(HttpClient http, IOptions<GoogleCalendarOptions> settings) : IGoogleCalendarGateway
{
    private const string Root = "https://www.googleapis.com/calendar/v3/";
    private GoogleCalendarOptions Options => settings.Value;
    private static string Q(string value) => Uri.EscapeDataString(value);
    private static string Events(string calendar) => $"calendars/{Q(calendar)}/events";
    public string AuthorizationUrl(string state) => "https://accounts.google.com/o/oauth2/v2/auth?" +
        $"client_id={Q(Options.ClientId)}&redirect_uri={Q(Options.RedirectUri)}&response_type=code&access_type=offline&prompt=consent&" +
        $"include_granted_scopes=true&scope={Q("openid email https://www.googleapis.com/auth/calendar.app.created")}&state={Q(state)}";

    private async Task<JsonElement> SendAsync(HttpMethod method, string url, string? token, object? payload, string? etag, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        if (payload is HttpContent content) request.Content = content;
        else if (payload is not null) request.Content = JsonContent.Create(payload);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var reason = "provider_error";
            try
            {
                using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var value = error.RootElement.GetProperty("error");
                reason = value.ValueKind == JsonValueKind.String ? value.GetString()! :
                    value.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0 ? S(errors[0], "reason") : "provider_error";
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
            // Provider bodies can contain personal data; never propagate them.
            var stage = url == "https://oauth2.googleapis.com/token" ? "token" :
                url == "https://openidconnect.googleapis.com/v1/userinfo" ? "identity" : "calendar";
            throw new CalendarProviderException((int)response.StatusCode, reason, stage);
        }
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0) return default;
        var text = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(text)) return default;
        using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone();
    }

    public async Task<CalendarGrant> ExchangeAsync(string code, CancellationToken ct)
    {
        var result = await SendAsync(HttpMethod.Post, "https://oauth2.googleapis.com/token", null,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = code, ["client_id"] = Options.ClientId,
                ["client_secret"] = Options.ClientSecret, ["redirect_uri"] = Options.RedirectUri, ["grant_type"] = "authorization_code" }), null, ct);
        var granted = S(result, "scope").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (!granted.Contains("https://www.googleapis.com/auth/calendar.app.created") && !granted.Contains("https://www.googleapis.com/auth/calendar"))
            throw new CalendarProviderException(403, "missing_scope", "permission");
        var token = S(result, "access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new CalendarProviderException(502, "missing_token", "token");
        var identity = await SendAsync(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo", token, null, null, ct);
        if (string.IsNullOrWhiteSpace(S(identity, "sub"))) throw new CalendarProviderException(401, "invalid_identity", "identity");
        return new(token, NullS(result, "refresh_token"), S(identity, "sub"), S(identity, "email"));
    }
    public async Task<string> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var result = await SendAsync(HttpMethod.Post, "https://oauth2.googleapis.com/token", null,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["refresh_token"] = refreshToken, ["client_id"] = Options.ClientId,
                ["client_secret"] = Options.ClientSecret, ["grant_type"] = "refresh_token" }), null, ct);
        return S(result, "access_token");
    }
    public async Task<string> CreateCalendarAsync(string accessToken, CancellationToken ct)
    {
        var result = await SendAsync(HttpMethod.Post, Root + "calendars", accessToken,
            new { summary = "PLANORA", timeZone = "Asia/Ho_Chi_Minh", description = "Lịch đồng bộ hai chiều với PLANORA." }, null, ct);
        return S(result, "id");
    }
    public async Task<CalendarPage> ListAsync(string accessToken, string calendarId, string? syncToken, string? pageToken, CancellationToken ct)
    {
        // All pages and token-based requests keep identical permitted filters.
        var url = Root + Events(calendarId) + "?showDeleted=true&singleEvents=false&maxResults=250";
        if (syncToken is not null) url += "&syncToken=" + Q(syncToken);
        if (pageToken is not null) url += "&pageToken=" + Q(pageToken);
        var result = await SendAsync(HttpMethod.Get, url, accessToken, null, null, ct);
        var zone = NullS(result, "timeZone") ?? "Asia/Ho_Chi_Minh";
        return new(result.TryGetProperty("items", out var items) ? items.EnumerateArray().Select(x => ParseEvent(x, zone)).ToArray() : [],
            NullS(result, "nextPageToken"), NullS(result, "nextSyncToken"));
    }
    public async Task<CalendarRemoteEvent?> GetAsync(string accessToken, string calendarId, string eventId, CancellationToken ct)
    {
        try
        {
            var result = await SendAsync(HttpMethod.Get, Root + Events(calendarId) + "/" + Q(eventId), accessToken, null, null, ct);
            var zone = "Asia/Ho_Chi_Minh";
            if (result.TryGetProperty("start", out var start) && start.TryGetProperty("date", out _))
                zone = NullS(await SendAsync(HttpMethod.Get, Root + "calendars/" + Q(calendarId), accessToken, null, null, ct), "timeZone") ?? zone;
            return ParseEvent(result, zone);
        }
        catch (CalendarProviderException error) when (error.Status is 404 or 410) { return null; }
    }
    public async Task<IReadOnlyList<CalendarRemoteEvent>> InstancesAsync(string accessToken, string calendarId, string eventId,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var events = new List<CalendarRemoteEvent>(); string? page = null;
        do
        {
            var url = Root + Events(calendarId) + "/" + Q(eventId) + "/instances?showDeleted=true&maxResults=250&timeMin=" +
                Q(start.ToString("O")) + "&timeMax=" + Q(end.ToString("O")) + (page is null ? "" : "&pageToken=" + Q(page));
            var result = await SendAsync(HttpMethod.Get, url, accessToken, null, null, ct);
            var zone = NullS(result, "timeZone") ?? "Asia/Ho_Chi_Minh";
            if (result.TryGetProperty("items", out var items)) events.AddRange(items.EnumerateArray().Select(x => ParseEvent(x, zone)));
            page = NullS(result, "nextPageToken");
        } while (page is not null);
        return events;
    }
    public async Task<CalendarRemoteEvent> WriteAsync(string accessToken, string calendarId, string eventId, CalendarSnapshot snapshot,
        string? etag, CalendarReminderInput? reminders, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?> { ["summary"] = snapshot.Title,
            ["start"] = new { dateTime = snapshot.Start.ToString("O"), timeZone = "Asia/Ho_Chi_Minh" },
            ["end"] = new { dateTime = snapshot.End.ToString("O"), timeZone = "Asia/Ho_Chi_Minh" } };
        if (etag is null) payload["id"] = eventId;
        if (reminders is not null)
        {
            var overrides = new List<object> { new { method = "popup", minutes = reminders.ReminderMinutes } };
            if (reminders.RemindAtStart && reminders.ReminderMinutes != 0) overrides.Add(new { method = "popup", minutes = 0 });
            payload["reminders"] = new { useDefault = false, overrides };
        }
        return ParseEvent(await SendAsync(etag is null ? HttpMethod.Post : HttpMethod.Patch,
            Root + Events(calendarId) + (etag is null ? "" : "/" + Q(eventId)), accessToken, payload, etag, ct));
    }
    public async Task DeleteAsync(string accessToken, string calendarId, string eventId, string etag, CancellationToken ct)
    {
        try { await SendAsync(HttpMethod.Delete, Root + Events(calendarId) + "/" + Q(eventId), accessToken, null, etag, ct); }
        catch (CalendarProviderException e) when (e.Status is 404 or 410) { }
    }
    public async Task<CalendarWatchResult> WatchAsync(string accessToken, string calendarId, string channelId, string token, CancellationToken ct)
    {
        var result = await SendAsync(HttpMethod.Post, Root + Events(calendarId) + "/watch", accessToken,
            new { id = channelId, type = "web_hook", address = Options.WebhookUrl, token, @params = new { ttl = "604800" } }, null, ct);
        return new(S(result, "resourceId"), DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(S(result, "expiration"), CultureInfo.InvariantCulture)));
    }
    public async Task StopWatchAsync(string accessToken, string channelId, string resourceId, CancellationToken ct) =>
        _ = await SendAsync(HttpMethod.Post, Root + "channels/stop", accessToken, new { id = channelId, resourceId }, null, ct);
    public async Task RevokeAsync(string refreshToken, CancellationToken ct) =>
        _ = await SendAsync(HttpMethod.Post, "https://oauth2.googleapis.com/revoke", null,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken }), null, ct);

    public static CalendarRemoteEvent ParseEvent(JsonElement value, string zoneId = "Asia/Ho_Chi_Minh")
    {
        var id = S(value, "id"); var etag = S(value, "etag"); var series = NullS(value, "recurringEventId");
        if (S(value, "status") == "cancelled") return new(id, etag, null, true, SeriesId: series);
        if (value.TryGetProperty("recurrence", out _)) return new(id, etag, null, false, true, IsSeries: true);
        if (!value.TryGetProperty("start", out var start) || !value.TryGetProperty("end", out var end))
            throw new CalendarProviderException(422, "invalid_event_time");
        var allDay = start.TryGetProperty("date", out _);
        var title = S(value, "summary"); if (title.Length == 0) title = "Sự kiện Google Calendar";
        // Local titles are bounded; the full remote title stays in Google.
        var readOnly = allDay || series is not null || title.Length > 200;
        return new(id, etag, new(title[..Math.Min(title.Length, 200)], ParseTime(start, zoneId), ParseTime(end, zoneId)), false, readOnly, series);
    }
    private static DateTimeOffset ParseTime(JsonElement value, string zoneId)
    {
        if (value.TryGetProperty("dateTime", out var dateTime)) return DateTimeOffset.Parse(dateTime.GetString()!, CultureInfo.InvariantCulture);
        var date = DateTime.SpecifyKind(DateTime.ParseExact(S(value, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); return new(date, zone.GetUtcOffset(date));
    }
    private static string S(JsonElement value, string key) => NullS(value, key) ?? "";
    private static string? NullS(JsonElement value, string key) => value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
}
