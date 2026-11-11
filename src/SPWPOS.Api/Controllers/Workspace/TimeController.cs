using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/time"), Tags("Time & Calendar")]
public sealed class TimeController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("/api/workspace/calendar/export.ics")]
    public async Task<IActionResult> Export(CancellationToken ct) => File(System.Text.Encoding.UTF8.GetBytes(await service.ExportCalendarAsync(UserId, ct)), "text/calendar; charset=utf-8", "planora.ics");
    [HttpGet("calendar")] public async Task<IActionResult> Calendar(CancellationToken ct) { var state = await service.GetAsync(UserId, ct); return Data(new { state.Events, state.Sessions }); }
    [HttpPut("availability")] public async Task<IActionResult> Weekly(WeeklyInput input, CancellationToken ct) { await service.SetAvailabilityAsync(UserId, input, ct); return Done(); }
    [HttpPost("exceptions")] public async Task<IActionResult> AddException(ExceptionInput input, CancellationToken ct) => Data(new { id = await service.SaveExceptionAsync(UserId, null, input, ct) });
    [HttpPut("exceptions/{id:guid}")] public async Task<IActionResult> UpdateException(Guid id, ExceptionInput input, CancellationToken ct) { await service.SaveExceptionAsync(UserId, id, input, ct); return Done(); }
    [HttpDelete("exceptions/{id:guid}")] public async Task<IActionResult> DeleteException(Guid id, CancellationToken ct) { await service.DeleteExceptionAsync(UserId, id, ct); return Done(); }
    [HttpPost("events")] public async Task<IActionResult> AddEvent(EventInput input, CancellationToken ct) => Data(new { id = await service.SaveEventAsync(UserId, null, input, ct) });
    [HttpPost("events/replan/preview")] public async Task<IActionResult> PreviewEventReplan(EventInput input, CancellationToken ct) =>
        Data(await service.PreviewEventReplanAsync(UserId, input, ct));
    [HttpPost("events/replan/{id:guid}/confirm")] public async Task<IActionResult> ConfirmEventReplan(Guid id, CancellationToken ct) =>
        Data(new { id = await service.ConfirmEventReplanAsync(UserId, id, ct) });
    [HttpPut("events/{id:guid}")] public async Task<IActionResult> UpdateEvent(Guid id, EventInput input, CancellationToken ct) { await service.SaveEventAsync(UserId, id, input, ct); return Done(); }
    [HttpDelete("events/{id:guid}")] public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken ct) { await service.DeleteEventAsync(UserId, id, ct); return Done(); }
    [HttpPut("preferences")] public async Task<IActionResult> Preferences(PreferenceInput input, CancellationToken ct) { await service.SetPreferenceAsync(UserId, input, ct); return Done(); }
}
