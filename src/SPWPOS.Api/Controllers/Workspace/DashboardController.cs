using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/workspace"), Tags("Workspace")]
public sealed class DashboardController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data(await service.GetAsync(UserId, ct));
    [HttpGet("gamification/profile")] public async Task<IActionResult> Gamification(CancellationToken ct) => Data(await service.GetGamificationAsync(UserId, ct));
    [HttpPost("gamification/skin")] public async Task<IActionResult> Skin(SkinInput input, CancellationToken ct) { await service.SelectSkinAsync(UserId, input, ct); return Done(); }
    [HttpPut("profile")] public async Task<IActionResult> Profile(ProfileInput input, CancellationToken ct) { await service.UpdateProfileAsync(UserId, input, ct); return Done(); }
}
