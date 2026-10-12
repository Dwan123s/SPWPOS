using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Infrastructure.Persistence;

namespace SPWPOS.Api.Controllers;

[ApiController, Route("api/browser-session/firebase"), Tags("Browser Session"), EnableRateLimiting("auth-ip")]
public sealed class FirebaseSessionController(IFirebaseTokenVerifier verifier, IAuthService auth, ApplicationDbContext db, IOptions<FirebaseSettings> options) : ControllerBase
{
    private RequestContext Context => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var settings = options.Value;
        var authenticated = Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var linked = authenticated && await db.UserLogins.AsNoTracking().AnyAsync(x => x.UserId == userId && x.LoginProvider == "FirebaseGoogle", ct);
        var hasPassword = authenticated && await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.PasswordHash != null, ct);
        return Ok(ApiResponse<object>.Ok(new { enabled = settings.Enabled, linked, hasPassword,
            config = settings.Enabled ? new { settings.ProjectId, settings.ApiKey, settings.AppId, settings.AuthDomain } : null }));
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(FirebaseSessionRequest input, CancellationToken ct)
    {
        EnsureOrigin();
        var identity = await verifier.VerifyGoogleAsync(input.IdToken, ct);
        var session = await auth.GoogleLoginAsync(identity, Context, ct);
        BrowserSessionController.SetSessionCookies(Response, session);
        return Ok(ApiResponse<object>.Ok(new { session.ExpiresAt }));
    }
    [Authorize, HttpPost("link")]
    public async Task<IActionResult> Link(FirebaseSessionRequest input, CancellationToken ct)
    {
        EnsureOrigin();
        var identity = await verifier.VerifyGoogleAsync(input.IdToken, ct);
        await auth.LinkGoogleAsync(Guid.Parse(User.FindFirst("sub")!.Value), identity, Context, ct);
        return Ok(ApiResponse<MessageResponse>.Ok(new("Đã liên kết Google với tài khoản của bạn.")));
    }
    private void EnsureOrigin()
    {
        if (Request.Headers["X-SPWPOS"] != "1" || !string.Equals(Request.Headers.Origin.ToString(), $"{Request.Scheme}://{Request.Host}", StringComparison.OrdinalIgnoreCase))
            throw new AuthException("CSRF_REJECTED", "Không thể xác minh nguồn yêu cầu.", 403);
    }
}
public sealed record FirebaseSessionRequest([Required, StringLength(8192, MinimumLength = 1)] string IdToken);
