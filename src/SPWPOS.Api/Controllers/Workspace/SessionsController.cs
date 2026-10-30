using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/sessions"), Tags("Execution")]
public sealed class SessionsController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("needs-review")] public async Task<IActionResult> CatchUp(CancellationToken ct) => Data(await service.GetCatchUpAsync(UserId, ct));
    [HttpPost("{id:guid}/catch-up")] public async Task<IActionResult> CatchUp(Guid id, CatchUpInput input, CancellationToken ct) => Data(await service.CatchUpAsync(UserId, id, input, ct));
    [HttpGet("{id:guid}/execution")] public async Task<IActionResult> History(Guid id, CancellationToken ct) => Data(await service.GetExecutionAsync(UserId, id, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Move(Guid id, SessionEdit input, CancellationToken ct) { await service.EditSessionAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("{id:guid}/lock")] public async Task<IActionResult> Lock(Guid id, CancellationToken ct) { await service.LockSessionAsync(UserId, id, true, ct); return Done(); }
    [HttpPost("{id:guid}/unlock")] public async Task<IActionResult> Unlock(Guid id, CancellationToken ct) { await service.LockSessionAsync(UserId, id, false, ct); return Done(); }
    [HttpPost("{id:guid}/split")] public async Task<IActionResult> Split(Guid id, SplitInput input, CancellationToken ct) { await service.SplitSessionAsync(UserId, id, input, ct); return Done(); }
    [HttpPut("{id:guid}/actual-duration")] public async Task<IActionResult> Duration(Guid id, DurationInput input, CancellationToken ct) { await service.RecordDurationAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("{id:guid}/{sessionAction:regex(^(start|pause|resume|complete|skip)$)}")] public async Task<IActionResult> Execute(Guid id, [FromRoute(Name = "sessionAction")] string action, CancellationToken ct) { await service.ExecuteSessionAsync(UserId, id, action, ct); return Done(); }
}
