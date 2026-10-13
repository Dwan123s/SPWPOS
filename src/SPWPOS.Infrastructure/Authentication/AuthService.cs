using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.Infrastructure.Security;
using SPWPOS.Infrastructure.Email;
namespace SPWPOS.Infrastructure.Authentication;

public sealed partial class AuthService(
    ApplicationDbContext db, UserManager<ApplicationUser> users, ILookupNormalizer normalizer,
    IPasswordHasher<ApplicationUser> hasher, PasswordTiming timing, IOtpService otps,
    IJwtTokenService tokens, IEmailService email, ISecurityAuditService audit, AccountRateLimiter limiter,
    IOptions<JwtSettings> jwtOptions, IOptions<OtpSettings> otpOptions, TimeProvider clock,
    ILogger<AuthService> logger, PasswordNotificationService notifications) : IAuthService
{
    public const string GenericEmailMessage = "If an account with this email exists, a verification code has been sent.";
    public async Task RegisterAsync(RegisterRequest request, RequestContext context, CancellationToken ct)
    {
        var userName = request.UserName?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(userName, @"\A[A-Za-z0-9][A-Za-z0-9._-]{2,31}\z",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            throw new AuthException("INVALID_USERNAME", "Tên đăng nhập cần 3–32 ký tự, bắt đầu bằng chữ hoặc số, chỉ gồm chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.");
        await ExecuteAsync(Normalize(request.Email), context, SecurityEventType.REGISTER, async () =>
        {
            await LimitAsync(request.Email, "register", 5, 15, null, ct);
            var existing = await FindEmailAsync(request.Email, ct);
            if (existing is not null) return true; // same public response, no duplicate account
            var normalizedUserName = normalizer.NormalizeName(userName)!;
            await AccountTransaction.LockUserNameAsync(db, normalizedUserName, ct);
            if (await db.Users.AnyAsync(x => x.NormalizedUserName == normalizedUserName, ct))
                throw new AuthException("USERNAME_TAKEN", "Tên đăng nhập đã được sử dụng. Hãy chọn tên khác.", 409);
            if (request.Password != request.ConfirmPassword) throw Validation();
            var now = clock.GetUtcNow();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = request.Email.Trim(),
                UserName = userName,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
                EmailConfirmed = false,
                IsActive = true,
                LockoutEnabled = true
            };
            await ValidatePasswordAsync(user, request.Password);
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) throw new InvalidOperationException("Identity account creation failed.");
            var displayName = (user.FirstName + " " + user.LastName).Trim();
            db.UserProfiles.Add(new() { UserId = user.Id, DisplayName = displayName[..Math.Min(displayName.Length, 100)], UpdatedAt = now });
            db.NotificationSettings.Add(new() { UserId = user.Id });
            var otp = await otps.GenerateOtpAsync(user.Id, OtpPurpose.EmailVerification, ct);
            await email.SendEmailVerificationOtpAsync(user.Email!, otp, ct);
            audit.Record(user.Id, SecurityEventType.REGISTER, true, context);
            return true;
        }, ct);
    }
    public async Task VerifyEmailAsync(OtpRequest request, RequestContext context, CancellationToken ct)
    {
        await ExecuteAsync(Normalize(request.Email), context, SecurityEventType.EMAIL_VERIFICATION_FAILED, async () =>
        {
            await LimitAsync(request.Email, "verify-email", 10, 1, null, ct);
            var user = await FindEmailAsync(request.Email, ct) ?? throw OtpInvalid();
            if (!user.IsActive || user.EmailConfirmed) throw OtpInvalid();
            await otps.VerifyOtpAsync(user.Id, OtpPurpose.EmailVerification, request.Otp, ct);
            user.EmailConfirmed = true; user.UpdatedAt = clock.GetUtcNow();
            await UpdateUserAsync(user);
            audit.Record(user.Id, SecurityEventType.EMAIL_VERIFICATION_SUCCESS, true, context);
            return true;
        }, ct);
    }
    public Task ResendVerificationOtpAsync(EmailRequest request, RequestContext context, CancellationToken ct) =>
        IssuePublicOtpAsync(request.Email, OtpPurpose.EmailVerification, context, ct);
    public Task ForgotPasswordAsync(EmailRequest request, RequestContext context, CancellationToken ct) =>
        IssuePublicOtpAsync(request.Email, OtpPurpose.ForgotPassword, context, ct);
    private async Task IssuePublicOtpAsync(string address, OtpPurpose purpose, RequestContext context, CancellationToken ct)
    {
        try
        {
            await ExecuteAsync(Normalize(address), context, SecurityEventType.OTP_FAILED, async () =>
            {
                await LimitAsync(address, "issue-" + purpose, 5, 15, TimeSpan.FromSeconds(otpOptions.Value.ResendCooldownSeconds), ct);
                var user = await FindEmailAsync(address, ct);
                bool eligible = user is { IsActive: true } && (purpose != OtpPurpose.EmailVerification || !user.EmailConfirmed);
                if (eligible)
                {
                    var otp = await otps.GenerateOtpAsync(user!.Id, purpose, ct);
                    if (purpose == OtpPurpose.EmailVerification) await email.SendEmailVerificationOtpAsync(user.Email!, otp, ct);
                    else await email.SendForgotPasswordOtpAsync(user.Email!, otp, ct);
                }
                audit.Record(user?.Id, purpose == OtpPurpose.ForgotPassword ? SecurityEventType.PASSWORD_RESET_REQUESTED : SecurityEventType.OTP_REQUESTED, true, context);
                return true;
            }, ct);
        }
        catch (AuthException ex) when (ex.StatusCode == 429)
        { /* Public requests deliberately have identical responses for absent/confirmed/cooling accounts. */ }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SMTP failure must not distinguish existing accounts. Transaction rolled back.
            logger.LogWarning("Public OTP request could not complete ({FailureType})", ex.GetType().Name);
        }
    }
    public async Task<TokenResponse> LoginAsync(LoginRequest request, RequestContext context, CancellationToken ct)
    {
        var identifier = request.Email.Trim();
        var normalizedIdentifier = identifier.Contains('@') ? Normalize(identifier) : normalizer.NormalizeName(identifier)!;
        var accountEmail = await FindLoginQuery(identifier, normalizedIdentifier).AsNoTracking().Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct);
        // Both aliases share the canonical account lock and persistent rate-limit bucket.
        var lockKey = accountEmail ?? Normalize(identifier);
        return await ExecuteAsync(lockKey, context, SecurityEventType.LOGIN_FAILED, async () =>
        {
            await LimitAsync(lockKey, "login", 10, 1, null, ct);
            var user = await FindLoginQuery(identifier, normalizedIdentifier).SingleOrDefaultAsync(ct);
            if (user is null || !user.IsActive)
            {
                hasher.VerifyHashedPassword(new ApplicationUser(), timing.DummyHash, request.Password);
                throw Credentials();
            }
            if (user.LockoutEnd > clock.GetUtcNow()) throw new AuthException("ACCOUNT_LOCKED", "Account is temporarily locked. Please try again later.", 423);
            if (user.PasswordHash is null)
            {
                hasher.VerifyHashedPassword(user, timing.DummyHash, request.Password);
                throw Credentials();
            }
            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = clock.GetUtcNow().AddMinutes(15); user.AccessFailedCount = 0;
                    audit.Record(user.Id, SecurityEventType.ACCOUNT_LOCKED, false, context);
                }
                user.UpdatedAt = clock.GetUtcNow();
                await UpdateUserAsync(user);
                throw Credentials();
            }
            // Only reveal EMAIL_NOT_VERIFIED after proving knowledge of the password.
            if (!user.EmailConfirmed) throw new AuthException("EMAIL_NOT_VERIFIED", "Verify your email before signing in.", 403);
            if (verification == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, request.Password);
            user.AccessFailedCount = 0; user.LockoutEnd = null;
            user.LastLoginAt = clock.GetUtcNow(); user.UpdatedAt = clock.GetUtcNow();
            await UpdateUserAsync(user);
            var response = await CreateSessionAsync(user, context, ct);
            audit.Record(user.Id, SecurityEventType.LOGIN_SUCCESS, true, context);
            return response;
        }, ct);
    }
    private IQueryable<ApplicationUser> FindLoginQuery(string identifier, string normalizedIdentifier) =>
        identifier.Contains('@') ? db.Users.Where(x => x.NormalizedEmail == normalizedIdentifier)
            : db.Users.Where(x => x.NormalizedUserName == normalizedIdentifier);
    public async Task<TokenResponse> RefreshTokenAsync(RefreshRequest request, RequestContext context, CancellationToken ct)
    {
        var hash = tokens.HashToken(request.RefreshToken);
        var owner = await db.RefreshTokens.AsNoTracking().Where(x => x.TokenHash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (owner is null) throw InvalidRefresh();
        return await ExecuteUserAsync(owner.Value, context, SecurityEventType.REFRESH_TOKEN, async user =>
        {
            await LimitAsync(user.Email!, "refresh", 30, 1, null, ct);
            var old = await db.RefreshTokens.SingleAsync(x => x.TokenHash == hash, ct);
            if (old.RevokedAt is not null)
            {
                await RevokeAsync(user.Id, old.FamilyId, ct);
                audit.Record(user.Id, SecurityEventType.REFRESH_TOKEN_REUSE, false, context);
                throw InvalidRefresh(); // committed family revocation on failure
            }
            if (old.ExpiresAt <= clock.GetUtcNow()) throw new AuthException("TOKEN_EXPIRED", "Session expired. Please sign in again.", 401);
            EnsureAvailable(user);
            old.RevokedAt = clock.GetUtcNow();
            var raw = tokens.GenerateRefreshToken();
            var replacement = NewRefresh(user.Id, old.FamilyId, raw, old.ExpiresAt, context);
            old.ReplacedByTokenId = replacement.Id; db.RefreshTokens.Add(replacement);
            var access = tokens.GenerateAccessToken(user, old.FamilyId, await users.GetRolesAsync(user));
            audit.Record(user.Id, SecurityEventType.REFRESH_TOKEN, true, context);
            return new TokenResponse(access.Token, raw, access.ExpiresAt);
        }, ct);
    }
    public async Task LogoutAsync(Guid userId, Guid familyId, RequestContext context, CancellationToken ct) =>
        await ExecuteUserAsync(userId, context, SecurityEventType.LOGOUT, async user =>
        {
            await RevokeAsync(user.Id, familyId, ct);
            audit.Record(user.Id, SecurityEventType.LOGOUT, true, context); return true;
        }, ct);
    public async Task LogoutAllAsync(Guid userId, RequestContext context, CancellationToken ct) =>
        await ExecuteUserAsync(userId, context, SecurityEventType.LOGOUT, async user =>
        {
            await RevokeAsync(user.Id, null, ct);
            user.SecurityStamp = Guid.NewGuid().ToString(); user.UpdatedAt = clock.GetUtcNow();
            await UpdateUserAsync(user);
            audit.Record(user.Id, SecurityEventType.LOGOUT, true, context); return true;
        }, ct);
    public Task<ResetTicketResponse> VerifyForgotPasswordOtpAsync(OtpRequest request, RequestContext context, CancellationToken ct) =>
        ExecuteAsync(Normalize(request.Email), context, SecurityEventType.OTP_FAILED, async () =>
        {
            await LimitAsync(request.Email, "verify-forgot", 10, 1, null, ct);
            var user = await FindEmailAsync(request.Email, ct) ?? throw OtpInvalid();
            if (!user.IsActive) throw OtpInvalid();
            await otps.VerifyOtpAsync(user.Id, OtpPurpose.ForgotPassword, request.Otp, ct);
            await InvalidateTicketsAsync(user.Id, ct);
            var raw = tokens.GenerateRefreshToken();
            db.PasswordResetTickets.Add(new PasswordResetTicket
            { UserId = user.Id, TokenHash = tokens.HashToken(raw), CreatedAt = clock.GetUtcNow(), ExpiresAt = clock.GetUtcNow().AddMinutes(10) });
            return new ResetTicketResponse(raw);
        }, ct);
    public async Task ResetPasswordAsync(ResetPasswordRequest request, RequestContext context, CancellationToken ct)
    {
        var hash = tokens.HashToken(request.ResetToken);
        var owner = await db.PasswordResetTickets.AsNoTracking().Where(x => x.TokenHash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (owner is null) throw InvalidReset();
        Guid recipient = await ExecuteUserAsync(owner.Value, context, SecurityEventType.PASSWORD_RESET_FAILED, async user =>
        {
            var ticket = await db.PasswordResetTickets.SingleAsync(x => x.TokenHash == hash, ct);
            if (ticket.UsedAt is not null || ticket.ExpiresAt <= clock.GetUtcNow() || ticket.Purpose != OtpPurpose.ForgotPassword) throw InvalidReset();
            if (!user.IsActive) throw Unauthorized();
            if (request.NewPassword != request.ConfirmNewPassword) throw Validation();
            await ValidatePasswordAsync(user, request.NewPassword);
            await ReplacePasswordAsync(user, request.NewPassword, ct);
            audit.Record(user.Id, SecurityEventType.PASSWORD_RESET_SUCCESS, true, context);
            return user.Id;
        }, ct);
        await NotifyPasswordChangedAsync(recipient, ct);
    }
    public async Task RequestChangePasswordOtpAsync(Guid userId, RequestContext context, CancellationToken ct) =>
        await ExecuteUserAsync(userId, context, SecurityEventType.OTP_FAILED, async user =>
        {
            EnsureAvailable(user);
            await LimitAsync(user.Email!, "issue-change", 5, 15, TimeSpan.FromSeconds(otpOptions.Value.ResendCooldownSeconds), ct);
            var otp = await otps.GenerateOtpAsync(user.Id, OtpPurpose.ChangePassword, ct);
            await email.SendChangePasswordOtpAsync(user.Email!, otp, ct);
            audit.Record(user.Id, SecurityEventType.CHANGE_PASSWORD_OTP_REQUESTED, true, context); return true;
        }, ct);
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, RequestContext context, CancellationToken ct)
    {
        var recipient = await ExecuteUserAsync(userId, context, SecurityEventType.CHANGE_PASSWORD_FAILED, async user =>
        {
            EnsureAvailable(user);
            await LimitAsync(user.Email!, "change-password", 10, 1, null, ct);
            if (user.LockoutEnd > clock.GetUtcNow()) throw new AuthException("ACCOUNT_LOCKED", "Account is temporarily locked.", 423);
            if (user.PasswordHash is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = clock.GetUtcNow().AddMinutes(2); user.AccessFailedCount = 0;
                    audit.Record(user.Id, SecurityEventType.ACCOUNT_LOCKED, false, context);
                }
                await UpdateUserAsync(user);
                throw Credentials();
            }
            if (request.NewPassword != request.ConfirmNewPassword) throw Validation();
            await ValidatePasswordAsync(user, request.NewPassword);
            await otps.VerifyOtpAsync(user.Id, OtpPurpose.ChangePassword, request.Otp, ct);
            await ReplacePasswordAsync(user, request.NewPassword, ct);
            audit.Record(user.Id, SecurityEventType.CHANGE_PASSWORD_SUCCESS, true, context);
            return user.Id;
        }, ct);
        await NotifyPasswordChangedAsync(recipient, ct);
    }
    public async Task SetPasswordAsync(Guid userId, SetPasswordRequest request, RequestContext context, CancellationToken ct)
    {
        var recipient = await ExecuteUserAsync(userId, context, SecurityEventType.CHANGE_PASSWORD_FAILED, async user =>
        {
            EnsureAvailable(user);
            await LimitAsync(user.Email!, "change-password", 10, 1, null, ct);
            if (user.LockoutEnd > clock.GetUtcNow()) throw new AuthException("ACCOUNT_LOCKED", "Tài khoản đang tạm khóa.", 423);
            if (user.PasswordHash is not null)
                throw new AuthException("PASSWORD_ALREADY_SET", "Tài khoản đã có mật khẩu. Hãy dùng phần đổi mật khẩu.", 409);
            if (request.NewPassword != request.ConfirmNewPassword) throw Validation();
            await ValidatePasswordAsync(user, request.NewPassword);
            await otps.VerifyOtpAsync(user.Id, OtpPurpose.ChangePassword, request.Otp, ct);
            await ReplacePasswordAsync(user, request.NewPassword, ct);
            audit.Record(user.Id, SecurityEventType.CHANGE_PASSWORD_SUCCESS, true, context);
            return user.Id;
        }, ct);
        await NotifyPasswordChangedAsync(recipient, ct);
    }
    public async Task<LoginMethodsResponse> GetLoginMethodsAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw Unauthorized();
        EnsureAvailable(user);
        return new(user.PasswordHash is not null, await db.UserLogins.AsNoTracking().AnyAsync(x => x.UserId == userId && x.LoginProvider == GoogleProvider, ct));
    }
    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw Unauthorized();
        EnsureAvailable(user);
        return new(user.Id, user.Email!, user.FirstName, user.LastName, user.EmailConfirmed, user.CreatedAt, user.LastLoginAt);
    }
    private async Task ReplacePasswordAsync(ApplicationUser user, string newPassword, CancellationToken ct)
    {
        user.PasswordHash = hasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString(); user.UpdatedAt = clock.GetUtcNow();
        user.AccessFailedCount = 0; user.LockoutEnd = null;
        await otps.InvalidateOtpAsync(user.Id, null, ct);
        await InvalidateTicketsAsync(user.Id, ct);
        await RevokeAsync(user.Id, null, ct);
        db.PasswordChangeNotifications.Add(new PasswordChangeNotification { UserId = user.Id, CreatedAt = clock.GetUtcNow(), NextAttemptAt = clock.GetUtcNow() });
        await UpdateUserAsync(user);
    }
    private async Task<TokenResponse> CreateSessionAsync(ApplicationUser user, RequestContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var family = Guid.NewGuid(); var raw = tokens.GenerateRefreshToken();
        db.RefreshTokens.Add(NewRefresh(user.Id, family, raw, clock.GetUtcNow().AddDays(jwtOptions.Value.RefreshTokenDays), context));
        var access = tokens.GenerateAccessToken(user, family, await users.GetRolesAsync(user));
        return new(access.Token, raw, access.ExpiresAt);
    }
    private RefreshToken NewRefresh(Guid userId, Guid familyId, string raw, DateTimeOffset expiresAt, RequestContext context) => new()
    {
        UserId = userId,
        FamilyId = familyId,
        TokenHash = tokens.HashToken(raw),
        CreatedAt = clock.GetUtcNow(),
        ExpiresAt = expiresAt,
        IpAddress = context.IpAddress?[..Math.Min(context.IpAddress.Length, 45)],
        DeviceInfo = context.UserAgent?[..Math.Min(context.UserAgent.Length, 256)]
    };
    private async Task RevokeAsync(Guid userId, Guid? familyId, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null && (familyId == null || x.FamilyId == familyId)).ToListAsync(ct);
        foreach (var token in active) token.RevokedAt = clock.GetUtcNow();
    }
    private async Task InvalidateTicketsAsync(Guid userId, CancellationToken ct)
    {
        var tickets = await db.PasswordResetTickets.Where(x => x.UserId == userId && x.UsedAt == null).ToListAsync(ct);
        foreach (var ticket in tickets) ticket.UsedAt = clock.GetUtcNow();
    }
    private async Task UpdateUserAsync(ApplicationUser user)
    {
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException("Identity account update failed.");
    }
    private async Task ValidatePasswordAsync(ApplicationUser user, string password)
    {
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            if (!result.Succeeded) throw new AuthException("PASSWORD_POLICY_FAILED", string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (user.PasswordHash is not null && hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed)
            throw new AuthException("PASSWORD_POLICY_FAILED", "New password must differ from your current password.");
    }
    private async Task NotifyPasswordChangedAsync(Guid userId, CancellationToken ct)
    {
        try { await notifications.SendPendingAsync(userId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { logger.LogError("Password change notification could not be delivered ({FailureType})", ex.GetType().Name); }
    }
    private async Task<T> ExecuteUserAsync<T>(Guid id, RequestContext context, SecurityEventType failureEvent, Func<ApplicationUser, Task<T>> action, CancellationToken ct)
    {
        var normalizedEmail = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct) ?? throw Unauthorized();
        return await ExecuteAsync(normalizedEmail, context, failureEvent, async () =>
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Unauthorized();
            return await action(user);
        }, ct);
    }
    private async Task<T> ExecuteAsync<T>(string normalizedEmail, RequestContext context, SecurityEventType failureEvent, Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = await AccountTransaction.BeginAsync(db, normalizedEmail, ct);
        try
        {
            var result = await action();
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
        }
        catch (AuthException ex)
        {
            // Persist security counters and reuse revocations even when the public operation fails.
            var id = await db.Users.Where(x => x.NormalizedEmail == normalizedEmail).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            audit.Record(id, failureEvent, false, context, ex.Code);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); throw;
        }
        // All infrastructure failures roll back the full transaction on disposal.
    }
    private Task<ApplicationUser?> FindEmailAsync(string address, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == Normalize(address), ct);
    private string Normalize(string address) => normalizer.NormalizeEmail(address.Trim())!;
    private Task LimitAsync(string address, string operation, int limit, int minutes, TimeSpan? cooldown, CancellationToken ct) =>
        limiter.CheckAsync(Normalize(address), operation, limit, TimeSpan.FromMinutes(minutes), cooldown, ct);
    private static void EnsureAvailable(ApplicationUser user)
    { if (!user.IsActive || !user.EmailConfirmed) throw Unauthorized(); }
    private static AuthException Credentials() => new("INVALID_CREDENTIALS", "Tên đăng nhập, email hoặc mật khẩu không đúng.", 401);
    private static AuthException Unauthorized() => new("UNAUTHORIZED", "Please sign in again.", 401);
    private static AuthException OtpInvalid() => new("OTP_INVALID", "Invalid or already used verification code.");
    private static AuthException InvalidRefresh() => new("UNAUTHORIZED", "Invalid session. Please sign in again.", 401);
    private static AuthException InvalidReset() => new("INVALID_RESET_TOKEN", "Invalid, expired, or already used reset token.");
    private static AuthException Validation() => new("VALIDATION_ERROR", "Password confirmation does not match.");
}
