using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
namespace SPWPOS.Infrastructure.Telegram;

public sealed class TelegramOptions
{
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = "";
    public int PollSeconds { get; set; } = 3;
    public string? ConfigurationError => !Enabled ? null
        : string.IsNullOrWhiteSpace(BotToken) ? "MISSING_BOT_TOKEN"
        : !Regex.IsMatch(BotToken, @"^\d+:[A-Za-z0-9_-]{20,}$", RegexOptions.CultureInvariant) ? "INVALID_BOT_TOKEN"
        : PollSeconds is < 2 or > 60 ? "INVALID_POLL_INTERVAL" : null;
    public bool Configured => Enabled && ConfigurationError is null;
    public bool Valid => !Enabled || Configured;
}
public sealed record TelegramUpdate(long Id, long ChatId, long SenderId, string ChatType, string Text, string DisplayName);
public interface ITelegramGateway
{
    Task<string> BotUsernameAsync(CancellationToken ct);
    Task<TelegramUpdate[]> UpdatesAsync(long offset, CancellationToken ct);
    Task SendAsync(long chatId, string html, CancellationToken ct);
}
// Never include Telegram's request URL or response description in errors: the URL contains the bot token.
public sealed class TelegramDeliveryException(int code, int retryAfterSeconds = 0) : Exception("Telegram request failed.")
{
    public int Code { get; } = code;
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
public sealed class TelegramGateway(HttpClient http, IOptions<TelegramOptions> options) : ITelegramGateway
{
    private async Task<JsonElement> Request(string method, object body, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync($"https://api.telegram.org/bot{options.Value.BotToken}/{method}", body, ct);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (!response.IsSuccessStatusCode || !root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            {
                var retry = root.TryGetProperty("parameters", out var p) && p.TryGetProperty("retry_after", out var r) ? r.GetInt32() : 0;
                throw new TelegramDeliveryException(root.TryGetProperty("error_code", out var e) ? e.GetInt32() : (int)response.StatusCode, retry);
            }
            return root.GetProperty("result").Clone();
        }
        catch (TelegramDeliveryException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new TelegramDeliveryException(0); }
    }
    public async Task<string> BotUsernameAsync(CancellationToken ct)
    {
        var bot = await Request("getMe", new { }, ct);
        var webhook = await Request("getWebhookInfo", new { }, ct);
        if (webhook.TryGetProperty("url", out var url) && !string.IsNullOrEmpty(url.GetString())) throw new TelegramDeliveryException(409);
        var name = bot.GetProperty("username").GetString() ?? "";
        if (!Regex.IsMatch(name, "^[A-Za-z0-9_]{5,32}$")) throw new TelegramDeliveryException(0);
        return name;
    }
    public async Task<TelegramUpdate[]> UpdatesAsync(long offset, CancellationToken ct)
    {
        var updates = await Request("getUpdates", new { offset, limit = 100, timeout = 0, allowed_updates = new[] { "message" } }, ct);
        return updates.EnumerateArray().Select(x =>
        {
            var id = x.GetProperty("update_id").GetInt64();
            if (!x.TryGetProperty("message", out var m) || !m.TryGetProperty("from", out var f)) return new TelegramUpdate(id, 0, 0, "", "", "");
            var chat = m.GetProperty("chat");
            var name = f.TryGetProperty("first_name", out var first) ? first.GetString() ?? "Telegram" : "Telegram";
            return new TelegramUpdate(id, chat.GetProperty("id").GetInt64(), f.GetProperty("id").GetInt64(),
                chat.GetProperty("type").GetString() ?? "", m.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "", name[..Math.Min(name.Length, 120)]);
        }).ToArray();
    }
    public async Task SendAsync(long chatId, string html, CancellationToken ct) =>
        _ = await Request("sendMessage", new { chat_id = chatId, text = html, parse_mode = "HTML", link_preview_options = new { is_disabled = true } }, ct);
}
