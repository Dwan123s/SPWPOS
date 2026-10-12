using SPWPOS.Application.Authentication.DTOs;
namespace SPWPOS.Application.Authentication.Interfaces;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request, RequestContext context, CancellationToken ct);
    Task VerifyEmailAsync(OtpRequest request, RequestContext context, CancellationToken ct);
    Task ResendVerificationOtpAsync(EmailRequest request, RequestContext context, CancellationToken ct);
    Task<TokenResponse> LoginAsync(LoginRequest request, RequestContext context, CancellationToken ct);
    Task<TokenResponse> GoogleLoginAsync(GoogleIdentity identity, RequestContext context, CancellationToken ct);
    Task LinkGoogleAsync(Guid userId, GoogleIdentity identity, RequestContext context, CancellationToken ct);
    Task<TokenResponse> RefreshTokenAsync(RefreshRequest request, RequestContext context, CancellationToken ct);
    Task LogoutAsync(Guid userId, Guid familyId, RequestContext context, CancellationToken ct);
    Task LogoutAllAsync(Guid userId, RequestContext context, CancellationToken ct);
    Task ForgotPasswordAsync(EmailRequest request, RequestContext context, CancellationToken ct);
    Task<ResetTicketResponse> VerifyForgotPasswordOtpAsync(OtpRequest request, RequestContext context, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, RequestContext context, CancellationToken ct);
    Task RequestChangePasswordOtpAsync(Guid userId, RequestContext context, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, RequestContext context, CancellationToken ct);
    Task SetPasswordAsync(Guid userId, SetPasswordRequest request, RequestContext context, CancellationToken ct);
    Task<LoginMethodsResponse> GetLoginMethodsAsync(Guid userId, CancellationToken ct);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct);
}
