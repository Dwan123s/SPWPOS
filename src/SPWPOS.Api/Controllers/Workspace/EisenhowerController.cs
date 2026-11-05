using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/eisenhower"), Tags("Eisenhower")]
public sealed class EisenhowerController(IEisenhowerService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data(await service.GetEisenhowerAsync(UserId, ct));
    [HttpPost("drafts")] public async Task<IActionResult> CreateDraft(MatrixDraftInput input, CancellationToken ct) => Data(new { id = await service.SaveMatrixDraftAsync(UserId, null, input, ct) });
    [HttpPut("drafts/{id:guid}")] public async Task<IActionResult> EditDraft(Guid id, MatrixDraftInput input, CancellationToken ct) => Data(new { id = await service.SaveMatrixDraftAsync(UserId, id, input, ct) });
    [HttpPost("drafts/{id:guid}/position")] public async Task<IActionResult> MoveDraft(Guid id, MatrixMoveInput input, CancellationToken ct) { await service.MoveMatrixDraftAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("tasks")] public async Task<IActionResult> Create(MatrixTaskInput input, CancellationToken ct) => Data(new { id = await service.SaveMatrixTaskAsync(UserId, null, input, ct) });
    [HttpPut("tasks/{id:guid}")] public async Task<IActionResult> Edit(Guid id, MatrixTaskInput input, CancellationToken ct) => Data(new { id = await service.SaveMatrixTaskAsync(UserId, id, input, ct) });
    [HttpPut("tasks/{id:guid}/checklist")] public async Task<IActionResult> Checklist(Guid id, MatrixChecklistInput input, CancellationToken ct) { await service.UpdateMatrixChecklistAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("tasks/{id:guid}/position")] public async Task<IActionResult> Move(Guid id, MatrixMoveInput input, CancellationToken ct) { await service.MoveMatrixTaskAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("tasks/{id:guid}/schedule")] public async Task<IActionResult> Schedule(Guid id, MatrixScheduleInput input, CancellationToken ct) => Data(await service.SuggestMatrixScheduleAsync(UserId, id, input, ct));
    [HttpGet("/api/tasks/{id:guid}/eisenhower")] public async Task<IActionResult> TaskAssessment(Guid id, CancellationToken ct) => Data(await service.GetTaskEisenhowerAsync(UserId, id, false, ct));
    [HttpGet("/api/teams/tasks/{id:guid}/eisenhower")] public async Task<IActionResult> TeamAssessment(Guid id, CancellationToken ct) => Data(await service.GetTaskEisenhowerAsync(UserId, id, true, ct));
    [HttpPost("/api/tasks/{id:guid}/importance")] public async Task<IActionResult> Set(Guid id, ImportanceInput input, CancellationToken ct) { await service.SetImportanceAsync(UserId, id, false, input, ct); return Done(); }
    [HttpPost("/api/tasks/{id:guid}/importance/proposal")] public async Task<IActionResult> Propose(Guid id, ImportanceProposalInput input, CancellationToken ct) => Data(await service.ProposeImportanceAsync(UserId, id, false, input, ct));
    [HttpPost("/api/teams/tasks/{id:guid}/importance/proposal")] public async Task<IActionResult> ProposeTeam(Guid id, ImportanceProposalInput input, CancellationToken ct) => Data(await service.ProposeImportanceAsync(UserId, id, true, input, ct));
    [HttpPost("/api/teams/tasks/{id:guid}/importance")] public async Task<IActionResult> SetTeam(Guid id, ImportanceInput input, CancellationToken ct) { await service.SetImportanceAsync(UserId, id, true, input, ct); return Done(); }
}
