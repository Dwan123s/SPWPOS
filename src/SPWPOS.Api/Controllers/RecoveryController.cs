using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;
[Route("api/recovery"),Tags("Planning")]
public sealed class RecoveryController(IWorkspaceService service):WorkspaceController
{
    [HttpGet]public async Task<IActionResult> Get(CancellationToken ct,int page=1)=>Data(await service.GetRecoveryAsync(UserId,page,ct));
    [HttpPut("settings")]public async Task<IActionResult> Settings(RecoverySettingInput input,CancellationToken ct){await service.SetRecoverySettingsAsync(UserId,input,ct);return Done();}
    [HttpPost("scan")]public async Task<IActionResult> Scan(CancellationToken ct){await service.ScanRecoveryAsync(UserId,ct);return Done();}
    [HttpPost("{id:guid}/rebuild")]public async Task<IActionResult> Rebuild(Guid id,CancellationToken ct){await service.RebuildRecoveryAsync(UserId,id,ct);return Done();}
    [HttpPost("{id:guid}/apply")]public async Task<IActionResult> Apply(Guid id,CancellationToken ct){await service.ApplyRecoveryAsync(UserId,id,ct);return Done();}
    [HttpPost("{id:guid}/dismiss")]public async Task<IActionResult> Dismiss(Guid id,CancellationToken ct){await service.DismissRecoveryAsync(UserId,id,ct);return Done();}
}
