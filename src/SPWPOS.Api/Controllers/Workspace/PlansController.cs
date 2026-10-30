using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/plans"), Tags("Planning")]
public sealed class PlansController(IWorkspaceService service) : WorkspaceController
{
    [HttpPost] public async Task<IActionResult> Generate(PlanInput input, CancellationToken ct) => Data(await service.GeneratePlanAsync(UserId, input, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Review(Guid id, CancellationToken ct) => Data(await service.GetPlanAsync(UserId, id, ct));
    [HttpGet("{id:guid}/items")] public async Task<IActionResult> Decisions(Guid id, CancellationToken ct) => Data(await service.GetPlanItemsAsync(UserId, id, ct));
    [HttpPost("{id:guid}/discard")] public async Task<IActionResult> Discard(Guid id, CancellationToken ct) { await service.DiscardPlanAsync(UserId, id, ct); return Done(); }
    [HttpPost("{id:guid}/apply")] public async Task<IActionResult> Apply(Guid id, CancellationToken ct) { await service.ApplyPlanAsync(UserId, id, ct); return Done(); }
    [HttpPost("reschedule")] public async Task<IActionResult> Reschedule(PlanInput input, CancellationToken ct) => Data(await service.GeneratePlanAsync(UserId, input with { IsReschedule = true }, ct));
    [HttpPost("conflicts")] public async Task<IActionResult> Conflicts(PlanInput input, CancellationToken ct) => Data(await service.DetectConflictsAsync(UserId, input, ct));
    [HttpPost("scenario")] public async Task<IActionResult> Scenario(ScenarioInput input, CancellationToken ct) => Data(await service.ScenarioAsync(UserId, input, ct));
}
