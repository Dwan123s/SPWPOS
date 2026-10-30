using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/notifications"), Tags("Notifications")]
public sealed class NotificationsController(IWorkspaceService service) : WorkspaceController
{
    [HttpPut("settings")] public async Task<IActionResult> Settings(NotificationSettingInput input, CancellationToken ct) { await service.SetNotificationSettingsAsync(UserId, input, ct); return Done(); }
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data(await service.GetNotificationsAsync(UserId, ct));
    [HttpPost("refresh")] public async Task<IActionResult> Refresh(CancellationToken ct) {
        await service.EnsureNotificationsAsync(UserId, ct);
        return Data(await service.GetNotificationsAsync(UserId, ct));
    }
    [HttpPost("{id:guid}/read")] public async Task<IActionResult> Read(Guid id, CancellationToken ct) { await service.MarkNotificationAsync(UserId, id, ct); return Done(); }
}
