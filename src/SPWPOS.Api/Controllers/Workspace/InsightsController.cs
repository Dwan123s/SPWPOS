using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/intelligence"), Tags("Insights")]
public sealed class InsightsController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("history")] public async Task<IActionResult> History([FromQuery] AnalyticsInput input, CancellationToken ct) => Data(await service.GetAnalyticsAsync(UserId, input, ct));
    [HttpGet("tasks")] public async Task<IActionResult> Tasks(CancellationToken ct, int page = 1, int pageSize = 20, string? search = null) => Data(await service.GetTaskPerformanceAsync(UserId, page, pageSize, search, ct));
    [HttpPost] public async Task<IActionResult> Analyze(PlanInput input, CancellationToken ct) => Data(await service.AnalyzeAsync(UserId, input, ct));
}
