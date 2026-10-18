using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;
namespace SPWPOS.Application.Authentication.Interfaces;
// OTP state is staged in the current DbContext. Callers must hold the account transaction
// through SaveChanges + Commit, including failed verification attempts.
public interface IOtpService
{
    Task<string> GenerateOtpAsync(Guid userId, OtpPurpose purpose, CancellationToken ct);
    Task VerifyOtpAsync(Guid userId, OtpPurpose purpose, string otp, CancellationToken ct);
    Task InvalidateOtpAsync(Guid userId, OtpPurpose? purpose, CancellationToken ct);
}
public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(ApplicationUser user, Guid familyId, IEnumerable<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
}
public interface IEmailService
{
    Task SendEmailVerificationOtpAsync(string email, string otp, CancellationToken ct);
    Task SendForgotPasswordOtpAsync(string email, string otp, CancellationToken ct);
    Task SendChangePasswordOtpAsync(string email, string otp, CancellationToken ct);
    Task SendPasswordChangedAsync(string email, CancellationToken ct);
    Task SendReminderAsync(string email, string title, string message, CancellationToken ct);
}
public interface ISecurityAuditService
{
    void Record(Guid? userId, SecurityEventType eventType, bool successful, RequestContext context, string? safeCode = null);
}
