using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class AdditionalFlowTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Forgot_wrong_OTP_records_attempt_and_does_not_issue_ticket()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        await h.AuthAsync(a => a.ForgotPasswordAsync(new(ServiceHarness.Address), ServiceHarness.Context, Ct));
        string otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)];
        var invalid = otp == "000000" ? "111111" : "000000";
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.VerifyForgotPasswordOtpAsync(new(ServiceHarness.Address, invalid), ServiceHarness.Context, Ct)));
        Assert.Empty(await h.DbAsync(db => db.PasswordResetTickets.ToListAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.EmailOtps.Where(x => x.Purpose == OtpPurpose.ForgotPassword).Select(x => x.AttemptCount).SingleAsync(Ct)));
    }
    [Fact]
    public async Task Unverified_account_can_reset_password_but_still_cannot_login()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(false);
        await h.AuthAsync(a => a.ForgotPasswordAsync(new(ServiceHarness.Address), ServiceHarness.Context, Ct));
        var code = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)];
        var ticket = await h.AuthAsync(a => a.VerifyForgotPasswordOtpAsync(new(ServiceHarness.Address, code), ServiceHarness.Context, Ct));
        await h.AuthAsync(a => a.ResetPasswordAsync(new(ticket.ResetToken, ServiceHarness.NewPassword, ServiceHarness.NewPassword), ServiceHarness.Context, Ct));
        Assert.False(await h.DbAsync(db => db.Users.Select(x => x.EmailConfirmed).SingleAsync(Ct)));
        Assert.Equal("EMAIL_NOT_VERIFIED", (await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync(ServiceHarness.NewPassword))).Code);
    }
    [Fact]
    public async Task Inactive_account_does_not_receive_password_reset_OTP()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        await h.DbAsync(async db => { var user = await db.Users.SingleAsync(Ct); user.IsActive = false; await db.SaveChangesAsync(Ct); });
        await h.AuthAsync(a => a.ForgotPasswordAsync(new(ServiceHarness.Address), ServiceHarness.Context, Ct));
        Assert.False(h.Email.Inbox.ContainsKey((ServiceHarness.Address, OtpPurpose.ForgotPassword)));
        Assert.Equal("INVALID_CREDENTIALS", (await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync())).Code);
    }
    [Fact]
    public async Task Invalid_new_password_does_not_consume_reset_ticket()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        await h.AuthAsync(a => a.ForgotPasswordAsync(new(ServiceHarness.Address), ServiceHarness.Context, Ct));
        var ticket = await h.AuthAsync(a => a.VerifyForgotPasswordOtpAsync(new(ServiceHarness.Address, h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)]), ServiceHarness.Context, Ct));
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.ResetPasswordAsync(new(ticket.ResetToken, "Password123!", "Password123!"), ServiceHarness.Context, Ct)));
        Assert.Null(await h.DbAsync(db => db.PasswordResetTickets.Select(x => x.UsedAt).SingleAsync(Ct)));
        await h.AuthAsync(a => a.ResetPasswordAsync(new(ticket.ResetToken, ServiceHarness.NewPassword, ServiceHarness.NewPassword), ServiceHarness.Context, Ct));
    }
    [Fact]
    public async Task Public_resend_cooldown_does_not_rotate_or_consume_existing_code()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(false);
        var original = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)];
        await h.AuthAsync(a => a.ResendVerificationOtpAsync(new(ServiceHarness.Address), ServiceHarness.Context, Ct));
        Assert.Equal(original, h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)]);
        Assert.Single(await h.DbAsync(db => db.EmailOtps.ToListAsync(Ct)));
        await h.AuthAsync(a => a.VerifyEmailAsync(new(ServiceHarness.Address, original), ServiceHarness.Context, Ct));
    }
    [Fact]
    public async Task Logout_only_revokes_selected_family_and_revoked_refresh_fails()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync(); var first = await h.LoginAsync(); var second = await h.LoginAsync();
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(first.AccessToken);
        var family = Guid.Parse(jwt.Claims.Single(x => x.Type == "sid").Value);
        await h.AuthAsync(a => a.LogoutAsync(user.Id, family, ServiceHarness.Context, Ct));
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a => a.RefreshTokenAsync(new(first.RefreshToken), ServiceHarness.Context, Ct)));
        await h.AuthAsync(a => a.RefreshTokenAsync(new(second.RefreshToken), ServiceHarness.Context, Ct));
    }
}
