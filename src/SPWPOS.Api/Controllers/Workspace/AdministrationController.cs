using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Authorize(Roles = "Admin"), Route("api/admin"), Tags("Administration")]
public sealed class AdministrationController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("metrics")] public async Task<IActionResult> Metrics(CancellationToken ct) => Data(await service.GetAdminMetricsAsync(ct));
    [HttpGet("health")] public async Task<IActionResult> Health(CancellationToken ct) => Data(await service.GetAdminHealthAsync(ct));
    [HttpGet("users")] public async Task<IActionResult> Users(CancellationToken ct, string? search = null) => Data(await service.SearchUsersAsync(search, ct));
    [HttpPost("users/{id:guid}/lock")] public async Task<IActionResult> Lock(Guid id, CancellationToken ct, [FromBody] AccountStatusInput? input = null) { await service.SetUserActiveAsync(UserId, id, false, ct, input?.Reason); return Done(); }
    [HttpPost("users/{id:guid}/unlock")] public async Task<IActionResult> Unlock(Guid id, CancellationToken ct, [FromBody] AccountStatusInput? input = null) { await service.SetUserActiveAsync(UserId, id, true, ct, input?.Reason); return Done(); }
    [HttpGet("users/{id:guid}/status-logs")] public async Task<IActionResult> AccountLogs(Guid id, CancellationToken ct) => Data(await service.GetAccountStatusLogsAsync(id, ct));
    [HttpGet("jobs")] public async Task<IActionResult> Jobs(CancellationToken ct) => Data(await service.GetJobRunsAsync(ct));
    [HttpGet("status")] public async Task<IActionResult> Status(CancellationToken ct) => Data(await service.SystemStatusAsync(ct));
}
