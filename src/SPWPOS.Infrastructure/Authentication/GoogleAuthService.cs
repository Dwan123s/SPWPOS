using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;

namespace SPWPOS.Infrastructure.Authentication;

public sealed partial class AuthService
{
    private const string GoogleProvider = "FirebaseGoogle";
    public async Task<TokenResponse> GoogleLoginAsync(GoogleIdentity identity, RequestContext context, CancellationToken ct)
    {
        ValidateGoogleIdentity(identity);
        var linked = await users.FindByLoginAsync(GoogleProvider, identity.Subject);
        if (linked is not null)
            return await ExecuteUserAsync(linked.Id, context, SecurityEventType.LOGIN_FAILED,
                user => CompleteGoogleLoginAsync(user, context, ct), ct);
        return await ExecuteAsync(Normalize(identity.Email), context, SecurityEventType.LOGIN_FAILED, async () =>
        {
            await LimitAsync(identity.Email, "google-login", 10, 1, null, ct);
            var existing = await FindEmailAsync(identity.Email, ct);
            if (existing is not null)
            {
                // Both the existing account and the server-verified Google identity must prove email ownership.
                // Never attach Google to an unverified registration with a potentially attacker-chosen password.
                if (!existing.EmailConfirmed)
                    throw new AuthException("GOOGLE_ACCOUNT_UNVERIFIED", "Email này đã đăng ký nhưng chưa xác minh. Hãy xác minh email của tài khoản trước khi đăng nhập Google.", 409);
                EnsureAvailable(existing);
                if (existing.LockoutEnd > clock.GetUtcNow()) throw new AuthException("ACCOUNT_LOCKED", "Tài khoản đang tạm khóa.", 423);
                var logins = await users.GetLoginsAsync(existing);
                if (logins.Any(x => x.LoginProvider == GoogleProvider && x.ProviderKey != identity.Subject))
                    throw new AuthException("GOOGLE_ALREADY_LINKED", "Tài khoản đã liên kết với một Google khác.", 409);
                if (!logins.Any(x => x.LoginProvider == GoogleProvider))
                {
                    if (!(await users.AddLoginAsync(existing, new UserLoginInfo(GoogleProvider, identity.Subject, "Google"))).Succeeded)
                        throw new InvalidOperationException("Google account association failed.");
                    audit.Record(existing.Id, SecurityEventType.GOOGLE_ACCOUNT_LINKED, true, context);
                }
                return await CompleteGoogleLoginAsync(existing, context, ct);
            }
            var now = clock.GetUtcNow();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), Email = identity.Email.Trim(), UserName = identity.Email.Trim(),
                FirstName = ShortName(identity.FirstName), LastName = ShortName(identity.LastName),
                EmailConfirmed = true, IsActive = true, LockoutEnabled = true, CreatedAt = now, UpdatedAt = now
            };
            if (!(await users.CreateAsync(user)).Succeeded) throw new InvalidOperationException("Google account creation failed.");
            if (!(await users.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, identity.Subject, "Google"))).Succeeded)
                throw new InvalidOperationException("Google account association failed.");
            var name = (user.FirstName + " " + user.LastName).Trim();
            db.UserProfiles.Add(new() { UserId = user.Id, DisplayName = name.Length == 0 ? "Người dùng Google" : name[..Math.Min(name.Length, 100)], UpdatedAt = now });
            db.NotificationSettings.Add(new() { UserId = user.Id });
            audit.Record(user.Id, SecurityEventType.REGISTER, true, context);
            return await CompleteGoogleLoginAsync(user, context, ct);
        }, ct);
    }
    public async Task LinkGoogleAsync(Guid userId, GoogleIdentity identity, RequestContext context, CancellationToken ct)
    {
        ValidateGoogleIdentity(identity);
        await ExecuteUserAsync(userId, context, SecurityEventType.LOGIN_FAILED, async user =>
        {
            EnsureAvailable(user);
            if (user.LockoutEnd > clock.GetUtcNow()) throw new AuthException("ACCOUNT_LOCKED", "Tài khoản đang tạm khóa.", 423);
            var linked = await users.FindByLoginAsync(GoogleProvider, identity.Subject);
            if (linked is not null && linked.Id != userId)
                throw new AuthException("GOOGLE_ALREADY_LINKED", "Google này đã liên kết với tài khoản khác.", 409);
            var logins = await users.GetLoginsAsync(user);
            if (logins.Any(x => x.LoginProvider == GoogleProvider && x.ProviderKey != identity.Subject))
                throw new AuthException("GOOGLE_ALREADY_LINKED", "Tài khoản của bạn đã liên kết với một Google khác.", 409);
            if (linked is null && !(await users.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, identity.Subject, "Google"))).Succeeded)
                throw new InvalidOperationException("Google account association failed.");
            audit.Record(user.Id, SecurityEventType.GOOGLE_ACCOUNT_LINKED, true, context);
            return true;
        }, ct);
    }
    private async Task<TokenResponse> CompleteGoogleLoginAsync(ApplicationUser user, RequestContext context, CancellationToken ct)
    {
        EnsureAvailable(user);
        if (user.LockoutEnd > clock.GetUtcNow()) throw new AuthException("ACCOUNT_LOCKED", "Tài khoản đang tạm khóa.", 423);
        user.AccessFailedCount = 0; user.LockoutEnd = null;
        user.LastLoginAt = clock.GetUtcNow(); user.UpdatedAt = clock.GetUtcNow();
        await UpdateUserAsync(user);
        var response = await CreateSessionAsync(user, context, ct);
        audit.Record(user.Id, SecurityEventType.LOGIN_SUCCESS, true, context);
        return response;
    }
    private static string ShortName(string value) => value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static void ValidateGoogleIdentity(GoogleIdentity identity)
    {
        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 128 ||
            identity.Email.Length > 254 || !MailAddress.TryCreate(identity.Email.Trim(), out var address) ||
            !string.Equals(address.Address, identity.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new AuthException("GOOGLE_IDENTITY_INVALID", "Cần tài khoản Google có email đã xác minh.", 401);
    }
}
