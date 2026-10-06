using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.Application.Authentication;
namespace SPWPOS.Api.Controllers;

[ApiController, Route("api/browser-session"), Tags("Browser Session"), EnableRateLimiting("auth-ip")]
public sealed class BrowserSessionController(IAuthService auth, IJwtTokenService tokens, ApplicationDbContext db) : ControllerBase
{
    public const string AccessCookie = "__Host-spwpos-access";
    private const string RefreshCookie = "__Secure-spwpos-refresh";
    private RequestContext Context => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    private static CookieOptions Options(DateTimeOffset expires, string path) => new() { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = path, Expires = expires, IsEssential = true };
    private void Set(TokenResponse response)
    {
        Response.Cookies.Append(AccessCookie, response.AccessToken, Options(response.ExpiresAt, "/"));
        Response.Cookies.Append(RefreshCookie, response.RefreshToken, Options(DateTimeOffset.UtcNow.AddDays(7), "/api/browser-session"));
    }
    private void Clear()
    {
        Response.Cookies.Delete(AccessCookie, Options(DateTimeOffset.UnixEpoch, "/"));
        Response.Cookies.Delete(RefreshCookie, Options(DateTimeOffset.UnixEpoch, "/api/browser-session"));
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest input, CancellationToken ct)
    { var response = await auth.LoginAsync(input, Context, ct); Set(response); return Ok(ApiResponse<object>.Ok(new { response.ExpiresAt })); }
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var token)) return Unauthorized();
        try { var response = await auth.RefreshTokenAsync(new(token), Context, ct); Set(response); return Ok(ApiResponse<object>.Ok(new { response.ExpiresAt })); }
        catch (AuthException error) when (error.StatusCode == 401) { Clear(); throw; }
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var token))
        {
            var hash = tokens.HashToken(token); var session = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
            if (session is not null) await auth.LogoutAsync(session.UserId, session.FamilyId, Context, ct);
        }
        Clear(); return Ok(ApiResponse<MessageResponse>.Ok(new("Đã đăng xuất.")));
    }
    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(ApiResponse<object>.Ok(new {
        user = await auth.GetCurrentUserAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct), roles = User.FindAll("role").Select(x => x.Value).ToArray() }));
}
