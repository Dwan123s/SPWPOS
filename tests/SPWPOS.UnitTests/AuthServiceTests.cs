using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class AuthServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Unverified_user_cannot_login()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(false);
        var error = await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync());
        Assert.Equal("EMAIL_NOT_VERIFIED", error.Code); Assert.Equal(403, error.StatusCode);
    }
    [Fact]
    public async Task Wrong_password_and_unknown_email_return_same_error()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        var wrong = await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync("wrong password"));
        var absent = await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.LoginAsync(new("absent@example.com", "wrong password"), ServiceHarness.Context, Ct)));
        Assert.Equal(absent.Code, wrong.Code); Assert.Equal(absent.Message, wrong.Message);
        Assert.Equal(1, await h.DbAsync(db => db.Users.Select(x => x.AccessFailedCount).SingleAsync(Ct)));
    }
    [Fact]
    public async Task Five_failures_lock_account_and_expiration_allows_login()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        for (int i = 0; i < 5; i++) await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync("wrong password"));
        Assert.Equal("ACCOUNT_LOCKED", (await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync())).Code);
        h.Clock.Advance(TimeSpan.FromMinutes(16)); await h.LoginAsync();
        var user = await h.DbAsync(db => db.Users.SingleAsync(Ct));
        Assert.Equal(0, user.AccessFailedCount); Assert.Null(user.LockoutEnd); Assert.NotNull(user.LastLoginAt);
    }
    [Fact]
    public async Task Login_returns_tokens_and_only_stores_refresh_hash()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); var session = await h.LoginAsync();
        Assert.NotEmpty(session.AccessToken); Assert.Equal(86, session.RefreshToken.Length);
        var token = await h.DbAsync(db => db.RefreshTokens.SingleAsync(Ct));
        Assert.NotEqual(session.RefreshToken, token.TokenHash); Assert.Equal(64, token.TokenHash.Length);
        Assert.Equal(TimeSpan.FromDays(7), token.ExpiresAt - token.CreatedAt);
    }
    [Theory]
    [InlineData("current", "INVALID_CREDENTIALS")]
    [InlineData("otp", "OTP_INVALID")]
    [InlineData("expired", "OTP_EXPIRED")]
    [InlineData("same", "PASSWORD_POLICY_FAILED")]
    public async Task Change_password_rejects_invalid_proofs(string scenario, string code)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); await h.LoginAsync();
        await h.AuthAsync(a => a.RequestChangePasswordOtpAsync(user.Id, ServiceHarness.Context, Ct));
        string otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)];
        if (scenario == "expired") h.Clock.Advance(TimeSpan.FromMinutes(6));
        var request = new ChangePasswordRequest(scenario == "current" ? "wrong password" : ServiceHarness.Password,
            scenario == "otp" ? (otp == "000000" ? "111111" : "000000") : otp,
            scenario == "same" ? ServiceHarness.Password : ServiceHarness.NewPassword,
            scenario == "same" ? ServiceHarness.Password : ServiceHarness.NewPassword);
        Assert.Equal(code, (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.ChangePasswordAsync(user.Id, request, ServiceHarness.Context, Ct)))).Code);
        await h.LoginAsync();
    }
    [Fact]
    public async Task Change_password_changes_hash_stamp_and_revokes_all_tokens()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await h.LoginAsync(); await h.LoginAsync();
        await h.AuthAsync(a => a.RequestChangePasswordOtpAsync(user.Id, ServiceHarness.Context, Ct));
        var otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)];
        await h.AuthAsync(a => a.ChangePasswordAsync(user.Id, new(ServiceHarness.Password, otp, ServiceHarness.NewPassword, ServiceHarness.NewPassword), ServiceHarness.Context, Ct));
        var updated = await h.DbAsync(db => db.Users.SingleAsync(Ct));
        Assert.NotEqual(user.PasswordHash, updated.PasswordHash); Assert.NotEqual(user.SecurityStamp, updated.SecurityStamp);
        Assert.True(await h.DbAsync(db => db.RefreshTokens.AllAsync(x => x.RevokedAt != null, Ct)));
        Assert.Equal(1, h.Email.Notifications);
        await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync()); await h.LoginAsync(ServiceHarness.NewPassword);
        Assert.Equal("OTP_INVALID", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.ChangePasswordAsync(user.Id, new(ServiceHarness.NewPassword, otp, "Another new passphrase 54!", "Another new passphrase 54!"), ServiceHarness.Context, Ct)))).Code);
    }
    [Fact]
    public async Task Request_change_otp_requires_cooldown()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await h.AuthAsync(a => a.RequestChangePasswordOtpAsync(user.Id, ServiceHarness.Context, Ct));
        Assert.Equal("OTP_RESEND_COOLDOWN", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.RequestChangePasswordOtpAsync(user.Id, ServiceHarness.Context, Ct)))).Code);
    }
    private static async Task<string> ResetTicketAsync(ServiceHarness h)
    {
        await h.AuthAsync(a => a.ForgotPasswordAsync(new(ServiceHarness.Address), ServiceHarness.Context, Ct));
        string otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)];
        var result = await h.AuthAsync(a => a.VerifyForgotPasswordOtpAsync(new(ServiceHarness.Address, otp), ServiceHarness.Context, Ct));
        return result.ResetToken;
    }
    [Fact]
    public async Task Forgot_OTP_is_consumed_and_reset_revokes_sessions()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); await h.LoginAsync();
        var ticket = await ResetTicketAsync(h);
        string otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)];
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.VerifyForgotPasswordOtpAsync(new(ServiceHarness.Address, otp), ServiceHarness.Context, Ct)));
        var request = new ResetPasswordRequest(ticket, ServiceHarness.NewPassword, ServiceHarness.NewPassword);
        await h.AuthAsync(a => a.ResetPasswordAsync(request, ServiceHarness.Context, Ct));
        Assert.True(await h.DbAsync(db => db.RefreshTokens.AllAsync(x => x.RevokedAt != null, Ct)));
        Assert.Equal("INVALID_RESET_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.ResetPasswordAsync(request, ServiceHarness.Context, Ct)))).Code);
        await h.LoginAsync(ServiceHarness.NewPassword);
    }
    [Fact]
    public async Task Expired_reset_ticket_is_rejected()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); var ticket = await ResetTicketAsync(h);
        h.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal("INVALID_RESET_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.ResetPasswordAsync(new(ticket, ServiceHarness.NewPassword, ServiceHarness.NewPassword), ServiceHarness.Context, Ct)))).Code);
    }
    [Fact]
    public async Task Forgot_password_for_absent_email_does_not_send_email()
    {
        await using var h = await ServiceHarness.CreateAsync();
        await h.AuthAsync(a => a.ForgotPasswordAsync(new("absent@example.com"), ServiceHarness.Context, Ct));
        Assert.Empty(h.Email.Inbox);
    }
    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_family_only()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); var first = await h.LoginAsync(); var other = await h.LoginAsync();
        var rotated = await h.AuthAsync(a => a.RefreshTokenAsync(new(first.RefreshToken), ServiceHarness.Context, Ct));
        Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);
        var rows = await h.DbAsync(db => db.RefreshTokens.ToListAsync(Ct));
        Assert.Equal(3, rows.Count); Assert.Single(rows, x => x.ReplacedByTokenId != null);
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.RefreshTokenAsync(new(first.RefreshToken), ServiceHarness.Context, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.RefreshTokenAsync(new(rotated.RefreshToken), ServiceHarness.Context, Ct)));
        await h.AuthAsync(a => a.RefreshTokenAsync(new(other.RefreshToken), ServiceHarness.Context, Ct));
        Assert.True(await h.DbAsync(db => db.SecurityAuditLogs.AnyAsync(x => x.EventType == SecurityEventType.REFRESH_TOKEN_REUSE, Ct)));
    }
    [Fact]
    public async Task Expired_refresh_is_rejected()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); var session = await h.LoginAsync();
        h.Clock.Advance(TimeSpan.FromDays(8));
        Assert.Equal("TOKEN_EXPIRED", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.RefreshTokenAsync(new(session.RefreshToken), ServiceHarness.Context, Ct)))).Code);
    }
    [Fact]
    public async Task SMTP_failure_rolls_back_registration()
    {
        await using var h = await ServiceHarness.CreateAsync(); h.Email.FailDelivery = true;
        await Assert.ThrowsAsync<IOException>(() => h.RegisterAsync());
        Assert.Empty(await h.DbAsync(db => db.Users.ToListAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.EmailOtps.ToListAsync(Ct)));
    }
    [Fact]
    public async Task Blocklisted_password_is_rejected()
    {
        await using var h = await ServiceHarness.CreateAsync();
        Assert.Equal("PASSWORD_POLICY_FAILED", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.RegisterAsync(new(ServiceHarness.Address, "Password123!", "Password123!", "N", "A"), ServiceHarness.Context, Ct)))).Code);
    }
}
