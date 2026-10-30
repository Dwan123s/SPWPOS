using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Calendar;
using SPWPOS.Infrastructure.Calendar;

namespace SPWPOS.Api.Controllers;

[Route("api/integrations/google-calendar"), Tags("Google Calendar")]
public sealed class GoogleCalendarController(IGoogleCalendarService service, ILogger<GoogleCalendarController> logger) : WorkspaceController
{
    [HttpGet("status")] public async Task<IActionResult> Status(CancellationToken ct) => Data(await service.StatusAsync(UserId, ct));
    [HttpPost("connect")] public async Task<IActionResult> Connect(CalendarReminderInput input, CancellationToken ct) => Data(await service.ConnectAsync(UserId, input, ct));
    [HttpPost("sync")] public async Task<IActionResult> Sync(CancellationToken ct) { await service.RequestSyncAsync(UserId, ct); return Done(); }
    [HttpPut("reminders")] public async Task<IActionResult> Reminders(CalendarReminderInput input, CancellationToken ct) { await service.UpdateRemindersAsync(UserId, input, ct); return Done(); }
    [HttpPost("disconnect")] public async Task<IActionResult> Disconnect(CancellationToken ct) { await service.DisconnectAsync(UserId, ct); return Done(); }
    [HttpGet("conflicts")] public async Task<IActionResult> Conflicts(CancellationToken ct) => Data(await service.ConflictsAsync(UserId, ct));
    [HttpPost("conflicts/{id:guid}/resolve")] public async Task<IActionResult> Resolve(Guid id, CalendarResolveInput input, CancellationToken ct)
    { await service.ResolveAsync(UserId, id, input.Keep, ct); return Done(); }

    [AllowAnonymous, HttpGet("callback")]
    public async Task<IActionResult> Callback(string? state, string? code, string? error, CancellationToken ct)
    {
        // Do not require the Strict login cookie on a cross-site OAuth return.
        // A one-use server-side state binds the callback to its initiating user.
        try
        {
            await service.CompleteOAuthAsync(state ?? "", code, error, ct);
            return Redirect("/?googleCalendar=connected#settings");
        }
        catch (AuthException ex) { return Redirect("/?googleCalendar=" + Uri.EscapeDataString(ex.Code) + "#settings"); }
        catch (CalendarProviderException ex)
        {
            var failureCode = CalendarOAuthErrors.Code(ex);
            logger.LogWarning("Google Calendar authorization failed: {FailureCode}; HTTP {ProviderStatus}; stage {Stage}.", failureCode, ex.Status, ex.Stage);
            return Redirect("/?googleCalendar=" + failureCode + "#settings");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("Google Calendar authorization failed: CALENDAR_NETWORK_ERROR; transport {FailureType}.", ex.GetType().Name);
            return Redirect("/?googleCalendar=CALENDAR_NETWORK_ERROR#settings");
        }
    }
    [AllowAnonymous, HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        await service.ReceiveWebhookAsync(Request.Headers["X-Goog-Channel-ID"].ToString(), Request.Headers["X-Goog-Resource-ID"].ToString(),
            Request.Headers["X-Goog-Channel-Token"].ToString(), ct);
        return NoContent();
    }
}
