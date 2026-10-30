using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Calendar;
using SPWPOS.Infrastructure.Calendar;

namespace SPWPOS.UnitTests;

public sealed class GoogleCalendarGatewayTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact] public void All_day_dates_keep_exclusive_end_and_recurring_instances_keep_their_identity()
    {
        using var doc = JsonDocument.Parse("""{"id":"all-day","etag":"v1","summary":"Ngày nghỉ","start":{"date":"2026-10-31"},"end":{"date":"2026-11-02"}}""");
        var result = GoogleCalendarGateway.ParseEvent(doc.RootElement);
        Assert.True(result.ReadOnly); Assert.Equal(48, (result.Snapshot!.End - result.Snapshot.Start).TotalHours);
        Assert.Equal(TimeSpan.FromHours(7), result.Snapshot.Start.Offset);
        using var instance = JsonDocument.Parse("""{"id":"series_20261031","etag":"v2","recurringEventId":"series","start":{"dateTime":"2026-10-31T10:00:00+07:00"},"end":{"dateTime":"2026-10-31T11:00:00+07:00"}}""");
        var occurrence = GoogleCalendarGateway.ParseEvent(instance.RootElement);
        Assert.True(occurrence.ReadOnly); Assert.Equal("series", occurrence.SeriesId); Assert.Equal("series_20261031", occurrence.Id);
    }
    [Fact] public async Task Patches_are_conditional_and_preserve_google_descriptions_attendees_and_reminders()
    {
        var handler = new InspectHandler(); var gateway = new GoogleCalendarGateway(new HttpClient(handler), Options.Create(new GoogleCalendarOptions()));
        var now = DateTimeOffset.Parse("2026-10-31T10:00:00+07:00");
        await gateway.WriteAsync("access-secret", "calendar@example.com", "eventid", new("Báo cáo", now, now.AddHours(1)), "\"etag-v1\"", null, Ct);
        Assert.Equal(HttpMethod.Patch, handler.Method); Assert.Equal("\"etag-v1\"", handler.IfMatch);
        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.False(payload.RootElement.TryGetProperty("reminders", out _));
        Assert.False(payload.RootElement.TryGetProperty("description", out _));
        Assert.False(payload.RootElement.TryGetProperty("attendees", out _));
        Assert.False(payload.RootElement.TryGetProperty("id", out _));
        await gateway.WriteAsync("access-secret", "calendar@example.com", "eventid", new("Báo cáo", now, now.AddHours(1)), null, new(15, true), Ct);
        using var created = JsonDocument.Parse(handler.Body!);
        Assert.Equal(HttpMethod.Post, handler.Method); Assert.Equal(2, created.RootElement.GetProperty("reminders").GetProperty("overrides").GetArrayLength());
    }
    [Fact] public void Authorization_requests_calendar_access_and_never_exposes_the_client_secret()
    {
        var gateway = new GoogleCalendarGateway(new HttpClient(), Options.Create(new GoogleCalendarOptions { ClientId = "client", ClientSecret = "private-secret" }));
        var url = Uri.UnescapeDataString(gateway.AuthorizationUrl("random-state"));
        Assert.Contains("calendar.app.created", url); Assert.Contains("access_type=offline", url); Assert.DoesNotContain("private-secret", url);
        Assert.Contains("include_granted_scopes=true", url);
    }
    [Theory]
    [InlineData("openid https://www.googleapis.com/auth/userinfo.email")]
    [InlineData("openid https://www.googleapis.com/auth/calendar.readonly")]
    public async Task Declined_calendar_permission_has_a_specific_error_before_fetching_identity(string granted)
    {
        var handler = new OAuthHandler(granted); var gateway = new GoogleCalendarGateway(new HttpClient(handler), Options.Create(new GoogleCalendarOptions()));
        var error = await Assert.ThrowsAsync<CalendarProviderException>(() => gateway.ExchangeAsync("test-code", Ct));
        Assert.Equal("CALENDAR_SCOPE_REQUIRED", CalendarOAuthErrors.Code(error)); Assert.Equal("permission", error.Stage);
        Assert.Equal(1, handler.Requests);
    }
    [Theory]
    [InlineData("openid  https://www.googleapis.com/auth/calendar.app.created\nhttps://www.googleapis.com/auth/userinfo.email")]
    [InlineData("openid https://www.googleapis.com/auth/calendar")]
    public async Task Granted_calendar_write_access_completes_the_exchange(string granted)
    {
        var handler = new OAuthHandler(granted); var gateway = new GoogleCalendarGateway(new HttpClient(handler), Options.Create(new GoogleCalendarOptions()));
        var grant = await gateway.ExchangeAsync("test-code", Ct);
        Assert.Equal("test-subject", grant.Subject); Assert.Equal("test-refresh", grant.RefreshToken); Assert.Equal(2, handler.Requests);
    }
    [Fact] public async Task Token_endpoint_errors_keep_diagnostics_but_never_return_provider_descriptions_or_credentials()
    {
        var handler = new OAuthHandler("", true); var gateway = new GoogleCalendarGateway(new HttpClient(handler), Options.Create(new GoogleCalendarOptions()));
        var error = await Assert.ThrowsAsync<CalendarProviderException>(() => gateway.ExchangeAsync("test-code", Ct));
        Assert.Equal("CALENDAR_CLIENT_REJECTED", CalendarOAuthErrors.Code(error)); Assert.Equal("token", error.Stage);
        Assert.DoesNotContain("private-canary", error.ToString());
        Assert.Equal("CALENDAR_PROVIDER_ERROR", CalendarOAuthErrors.Code(new(500, "private-canary")));
    }
    private sealed class OAuthHandler(string scope, bool reject = false) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (reject) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                { Content = new StringContent("""{"error":"invalid_client","error_description":"private-canary"}""", Encoding.UTF8, "application/json") });
            var payload = Requests == 1 ? JsonSerializer.Serialize(new { access_token = "test-access", refresh_token = "test-refresh", scope }) :
                """{"sub":"test-subject","email":"test@example.com"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class InspectHandler : HttpMessageHandler
    {
        public HttpMethod? Method; public string? Body; public string? IfMatch;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method; Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            IfMatch = request.Headers.TryGetValues("If-Match", out var values) ? values.Single() : null;
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"id":"eventid","etag":"v2","summary":"Báo cáo","start":{"dateTime":"2026-10-31T10:00:00+07:00"},"end":{"dateTime":"2026-10-31T11:00:00+07:00"}}""", Encoding.UTF8, "application/json") };
        }
    }
}
