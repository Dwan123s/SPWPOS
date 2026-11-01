using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/integrations/telegram"), Tags("Telegram")]
public sealed class TelegramController(ITelegramNotificationService service) : WorkspaceController
{
    [HttpGet("status")] public async Task<IActionResult> Status(CancellationToken ct) => Data(await service.StatusAsync(UserId, ct));
    [HttpPost("connect")] public async Task<IActionResult> Connect(CancellationToken ct) => Data(await service.ConnectAsync(UserId, ct));
    [HttpPut("preferences")] public async Task<IActionResult> Preferences(TelegramPreference input, CancellationToken ct)
    { await service.SetEnabledAsync(UserId, input.Enabled, ct); return Done(); }
    [HttpDelete] public async Task<IActionResult> Disconnect(CancellationToken ct)
    { await service.DisconnectAsync(UserId, ct); return Done(); }
}
