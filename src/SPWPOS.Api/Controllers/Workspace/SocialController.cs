using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/social"), Tags("Profiles and friendships")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SocialController(ISocialService service) : WorkspaceController
{
    [HttpGet("profiles/{id:guid}")]
    public async Task<IActionResult> Profile(Guid id, CancellationToken ct) => Data(await service.ProfileAsync(UserId, id, ct));
    [HttpGet("people")]
    public async Task<IActionResult> People([FromQuery] string? search, [FromQuery] int offset, CancellationToken ct)
        => Data(await service.PeopleAsync(UserId, search, offset, ct));
    [HttpGet("connections")]
    public async Task<IActionResult> Connections([FromQuery] string kind, [FromQuery] int offset, CancellationToken ct)
        => Data(await service.ConnectionsAsync(UserId, UserId, kind, offset, ct));
    [HttpGet("profiles/{id:guid}/friends")]
    public async Task<IActionResult> Friends(Guid id, [FromQuery] int offset, CancellationToken ct)
        => Data(await service.ConnectionsAsync(UserId, id, "friends", offset, ct));
    [HttpPost("friends/{id:guid}")]
    public async Task<IActionResult> SendRequest(Guid id, CancellationToken ct) => Data(await service.RequestAsync(UserId, id, ct));
    [HttpPost("friends/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct) => Data(await service.AcceptAsync(UserId, id, ct));
    [HttpDelete("friends/{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) => Data(await service.RemoveAsync(UserId, id, ct));
    [HttpPut("profile/cover"), RequestSizeLimit(3100000)]
    public async Task<IActionResult> Cover(ProfileCoverInput input, CancellationToken ct)
    { await service.UpdateCoverAsync(UserId, input, ct); return Done(); }
}
