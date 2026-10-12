using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Infrastructure.Authentication;
namespace SPWPOS.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth-ip")]
[RequestSizeLimit(16 * 1024)]
[ProducesResponseType(typeof(ProblemDetails), 400), ProducesResponseType(typeof(ProblemDetails), 401), ProducesResponseType(typeof(ProblemDetails), 429)]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    private RequestContext Context => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    private Guid FamilyId => Guid.Parse(User.FindFirstValue("sid")!);
    private static ApiResponse<MessageResponse> Message(string text) => ApiResponse<MessageResponse>.Ok(new(text));

    [HttpPost("register"), Tags("Authentication")]
    [EndpointSummary("Create an account and send an email OTP. No session is created.")]
    [ProducesResponseType(typeof(ApiResponse<MessageResponse>), 200)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    { await auth.RegisterAsync(request, Context, ct); return Ok(Message(AuthService.GenericEmailMessage)); }

    [HttpPost("verify-email"), Tags("Authentication")]
    [EndpointSummary("Consume a single-use EmailVerification OTP.")]
    public async Task<IActionResult> VerifyEmail(OtpRequest request, CancellationToken ct)
    { await auth.VerifyEmailAsync(request, Context, ct); return Ok(Message("Email verified successfully.")); }

    [HttpPost("resend-verification-otp"), Tags("Authentication")]
    [EndpointSummary("Request a new email verification OTP; previous code is invalidated.")]
    public async Task<IActionResult> ResendVerificationOtp(EmailRequest request, CancellationToken ct)
    { await auth.ResendVerificationOtpAsync(request, Context, ct); return Ok(Message(AuthService.GenericEmailMessage)); }

    [HttpPost("login"), Tags("Authentication")]
    [EndpointSummary("Sign in with a verified email and password.")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), 200), ProducesResponseType(typeof(ProblemDetails), 403), ProducesResponseType(typeof(ProblemDetails), 423)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) => Ok(ApiResponse<TokenResponse>.Ok(await auth.LoginAsync(request, Context, ct)));

    [HttpPost("refresh-token"), Tags("Authentication")]
    [EndpointSummary("Rotate a refresh token. Reusing a revoked token revokes its entire session.")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), 200)]
    public async Task<IActionResult> RefreshToken(RefreshRequest request, CancellationToken ct) => Ok(ApiResponse<TokenResponse>.Ok(await auth.RefreshTokenAsync(request, Context, ct)));

    [Authorize, HttpPost("logout"), Tags("Account")]
    [EndpointSummary("Revoke the session identified by the Bearer access token.")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    { await auth.LogoutAsync(UserId, FamilyId, Context, ct); return Ok(Message("Logged out successfully.")); }

    [Authorize, HttpPost("logout-all"), Tags("Account")]
    [EndpointSummary("Revoke all sessions on all devices.")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    { await auth.LogoutAllAsync(UserId, Context, ct); return Ok(Message("All sessions have been signed out.")); }

    [HttpPost("forgot-password"), Tags("Authentication")]
    [EndpointSummary("Request a ForgotPassword OTP; always returns a generic account message.")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    { await auth.ForgotPasswordAsync(request, Context, ct); return Ok(Message(AuthService.GenericEmailMessage)); }

    [HttpPost("verify-forgot-password-otp"), Tags("Authentication")]
    [EndpointSummary("Consume a ForgotPassword OTP and issue a single-use, ten-minute reset ticket.")]
    [ProducesResponseType(typeof(ApiResponse<ResetTicketResponse>), 200)]
    public async Task<IActionResult> VerifyForgotPasswordOtp(OtpRequest request, CancellationToken ct) => Ok(ApiResponse<ResetTicketResponse>.Ok(await auth.VerifyForgotPasswordOtpAsync(request, Context, ct)));

    [HttpPost("reset-password"), Tags("Authentication")]
    [EndpointSummary("Consume a reset ticket, change the password, and revoke every session.")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    { await auth.ResetPasswordAsync(request, Context, ct); return Ok(Message("Password reset successfully. Please login again.")); }

    [Authorize, HttpPost("change-password/request-otp"), Tags("Account")]
    [EndpointSummary("Send a ChangePassword OTP to the authenticated user's verified email.")]
    public async Task<IActionResult> RequestChangePasswordOtp(CancellationToken ct)
    { await auth.RequestChangePasswordOtpAsync(UserId, Context, ct); return Ok(Message("Verification code sent.")); }

    [Authorize, HttpPost("change-password"), Tags("Account")]
    [EndpointSummary("Require current password AND email OTP, then revoke all sessions.")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    { await auth.ChangePasswordAsync(UserId, request, Context, ct); return Ok(Message("Password changed successfully. Please login again.")); }

    [Authorize, HttpGet("login-methods"), Tags("Account")]
    public async Task<IActionResult> LoginMethods(CancellationToken ct)
        => Ok(ApiResponse<LoginMethodsResponse>.Ok(await auth.GetLoginMethodsAsync(UserId, ct)));

    [Authorize, HttpPost("set-password"), Tags("Account")]
    [EndpointSummary("Set the first password using email OTP, then revoke every session.")]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request, CancellationToken ct)
    { await auth.SetPasswordAsync(UserId, request, Context, ct); return Ok(Message("Đã thiết lập mật khẩu. Hãy đăng nhập lại.")); }

    [Authorize, HttpGet("me"), Tags("Account")]
    [EndpointSummary("Return the current account without security fields.")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), 200)]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(ApiResponse<CurrentUserResponse>.Ok(await auth.GetCurrentUserAsync(UserId, ct)));
}
