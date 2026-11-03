using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/eisenhower"), Tags("Eisenhower")]
public sealed class EisenhowerController(IEisenhowerService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data(await service.GetEisenhowerAsync(UserId, ct));
    [HttpGet("/api/tasks/{id:guid}/eisenhower")] public async Task<IActionResult> TaskAssessment(Guid id, CancellationToken ct) => Data(await service.GetTaskEisenhowerAsync(UserId, id, false, ct));
    [HttpGet("/api/teams/tasks/{id:guid}/eisenhower")] public async Task<IActionResult> TeamAssessment(Guid id, CancellationToken ct) => Data(await service.GetTaskEisenhowerAsync(UserId, id, true, ct));
    [HttpPost("/api/tasks/{id:guid}/importance")] public async Task<IActionResult> Set(Guid id, ImportanceInput input, CancellationToken ct) { await service.SetImportanceAsync(UserId, id, false, input, ct); return Done(); }
    [HttpPost("/api/tasks/{id:guid}/importance/proposal")] public async Task<IActionResult> Propose(Guid id, ImportanceProposalInput input, CancellationToken ct) => Data(await service.ProposeImportanceAsync(UserId, id, false, input, ct));
    [HttpPost("/api/teams/tasks/{id:guid}/importance/proposal")] public async Task<IActionResult> ProposeTeam(Guid id, ImportanceProposalInput input, CancellationToken ct) => Data(await service.ProposeImportanceAsync(UserId, id, true, input, ct));
    [HttpPost("/api/teams/tasks/{id:guid}/importance")] public async Task<IActionResult> SetTeam(Guid id, ImportanceInput input, CancellationToken ct) { await service.SetImportanceAsync(UserId, id, true, input, ct); return Done(); }
}
