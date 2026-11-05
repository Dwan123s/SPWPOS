using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Api.Controllers;

[Route("api/agent/reminders"), Tags("NORA reminders")]
public sealed class NoraRemindersController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Data(await service.GetNoraRemindersAsync(UserId, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    { await service.CancelNoraReminderAsync(UserId, id, ct); return Done(); }
}
