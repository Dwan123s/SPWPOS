using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class GoogleAuthTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static GoogleIdentity Identity(string email = "google@example.com") => new("firebase-user-123", email, true, "Minh", "Nguyễn");
    [Fact]
    public async Task New_google_user_gets_profile_and_session_and_reuses_stable_uid()
    {
        await using var h = await ServiceHarness.CreateAsync();
        var session = await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(), ServiceHarness.Context, Ct));
        Assert.NotEmpty(session.AccessToken);
        var user = await h.DbAsync(db => db.Users.SingleAsync(Ct));
        Assert.True(user.EmailConfirmed); Assert.Null(user.PasswordHash);
        Assert.Equal(user.Id, await h.DbAsync(db => db.UserProfiles.Select(x => x.UserId).SingleAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.NotificationSettings.CountAsync(Ct)));
        Assert.Equal("firebase-user-123", await h.DbAsync(db => db.UserLogins.Select(x => x.ProviderKey).SingleAsync(Ct)));
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity("changed@example.com"), ServiceHarness.Context, Ct));
        Assert.Equal(1, await h.DbAsync(db => db.Users.CountAsync(Ct)));
        Assert.Equal(2, await h.DbAsync(db => db.RefreshTokens.CountAsync(Ct)));
        Assert.Equal("google@example.com", (await h.DbAsync(db => db.Users.SingleAsync(Ct))).Email);
    }
    [Fact]
    public async Task Verified_local_email_automatically_links_google_and_preserves_profile_and_password()
    {
        await using var h = await ServiceHarness.CreateAsync(); var local = await h.RegisterAsync();
        await h.LoginAsync();
        await h.DbAsync(async db => { var profile = await db.UserProfiles.SingleAsync(Ct); profile.Bio = "Keep my workspace"; await db.SaveChangesAsync(Ct); });
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address.ToUpperInvariant()), ServiceHarness.Context, Ct));
        Assert.Equal(local.Id, await h.DbAsync(db => db.Users.Select(x => x.Id).SingleAsync(Ct)));
        Assert.Equal("Keep my workspace", await h.DbAsync(db => db.UserProfiles.Select(x => x.Bio).SingleAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.UserLogins.CountAsync(Ct)));
        Assert.Equal(2, await h.DbAsync(db => db.RefreshTokens.CountAsync(Ct)));
        await h.LoginAsync();
    }
    [Fact]
    public async Task Unverified_registration_is_not_automatically_linked()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(false);
        var error = await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address), ServiceHarness.Context, Ct)));
        Assert.Equal("GOOGLE_ACCOUNT_UNVERIFIED", error.Code);
        Assert.Equal(0, await h.DbAsync(db => db.UserLogins.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.RefreshTokens.CountAsync(Ct)));
    }
    [Theory]
    [InlineData("disabled")] [InlineData("locked")]
    public async Task Automatic_link_does_not_bypass_local_restrictions_or_commit_an_association(string restriction)
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        await h.DbAsync(async db => { var user = await db.Users.SingleAsync(Ct); if (restriction == "disabled") user.IsActive = false; else user.LockoutEnd = h.Clock.GetUtcNow().AddMinutes(15); await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address), ServiceHarness.Context, Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.UserLogins.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.RefreshTokens.CountAsync(Ct)));
    }
    [Fact]
    public async Task Automatic_link_cannot_replace_an_existing_google_uid()
    {
        await using var h = await ServiceHarness.CreateAsync(); var local = await h.RegisterAsync();
        await h.AuthAsync(auth => auth.LinkGoogleAsync(local.Id, Identity(ServiceHarness.Address), ServiceHarness.Context, Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address) with { Subject = "another-firebase-user" }, ServiceHarness.Context, Ct)));
        Assert.Equal("GOOGLE_ALREADY_LINKED", error.Code);
        Assert.Equal("firebase-user-123", await h.DbAsync(db => db.UserLogins.Select(x => x.ProviderKey).SingleAsync(Ct)));
    }
    [Fact]
    public async Task Google_account_sets_first_password_with_otp_and_keeps_both_login_methods()
    {
        await using var h = await ServiceHarness.CreateAsync();
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address), ServiceHarness.Context, Ct));
        var userId = await h.DbAsync(db => db.Users.Select(x => x.Id).SingleAsync(Ct));
        Assert.False((await h.AuthAsync(auth => auth.GetLoginMethodsAsync(userId, Ct))).HasPassword);
        await h.AuthAsync(auth => auth.RequestChangePasswordOtpAsync(userId, ServiceHarness.Context, Ct));
        var otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)];
        await h.AuthAsync(auth => auth.SetPasswordAsync(userId, new(otp, ServiceHarness.Password, ServiceHarness.Password), ServiceHarness.Context, Ct));
        var methods = await h.AuthAsync(auth => auth.GetLoginMethodsAsync(userId, Ct));
        Assert.True(methods.HasPassword); Assert.True(methods.GoogleLinked);
        Assert.Equal(0, await h.DbAsync(db => db.RefreshTokens.CountAsync(x => x.RevokedAt == null, Ct)));
        await h.LoginAsync();
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address), ServiceHarness.Context, Ct));
        Assert.Equal(userId, await h.DbAsync(db => db.Users.Select(x => x.Id).SingleAsync(Ct)));
        var overwrite = await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.SetPasswordAsync(userId, new(otp, ServiceHarness.NewPassword, ServiceHarness.NewPassword), ServiceHarness.Context, Ct)));
        Assert.Equal("PASSWORD_ALREADY_SET", overwrite.Code);
        await h.LoginAsync();
    }
    [Fact]
    public async Task First_password_rejects_invalid_otp_without_changing_account_or_session()
    {
        await using var h = await ServiceHarness.CreateAsync();
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address), ServiceHarness.Context, Ct));
        var id = await h.DbAsync(db => db.Users.Select(x => x.Id).SingleAsync(Ct));
        await h.AuthAsync(auth => auth.RequestChangePasswordOtpAsync(id, ServiceHarness.Context, Ct));
        var otp = h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)];
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.SetPasswordAsync(id, new(otp == "000000" ? "111111" : "000000", ServiceHarness.Password, ServiceHarness.Password), ServiceHarness.Context, Ct)));
        Assert.Null(await h.DbAsync(db => db.Users.Select(x => x.PasswordHash).SingleAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.RefreshTokens.CountAsync(x => x.RevokedAt == null, Ct)));
    }
    [Fact]
    public async Task Explicit_link_keeps_local_account_and_password()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await h.AuthAsync(auth => auth.LinkGoogleAsync(user.Id, Identity(), ServiceHarness.Context, Ct));
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(), ServiceHarness.Context, Ct));
        Assert.Equal(user.Id, await h.DbAsync(db => db.RefreshTokens.Select(x => x.UserId).SingleAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.Users.CountAsync(Ct)));
        await h.LoginAsync();
    }
    [Fact]
    public async Task A_google_identity_cannot_move_to_another_account()
    {
        await using var h = await ServiceHarness.CreateAsync();
        var local = await h.RegisterAsync();
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(), ServiceHarness.Context, Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.LinkGoogleAsync(local.Id, Identity(), ServiceHarness.Context, Ct)));
        Assert.Equal("GOOGLE_ALREADY_LINKED", error.Code);
    }
    [Theory]
    [InlineData("disabled")] [InlineData("locked")]
    public async Task Google_login_respects_local_account_restrictions(string restriction)
    {
        await using var h = await ServiceHarness.CreateAsync();
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(), ServiceHarness.Context, Ct));
        await h.DbAsync(async db => { var user = await db.Users.SingleAsync(Ct); if (restriction == "disabled") user.IsActive = false; else user.LockoutEnd = h.Clock.GetUtcNow().AddMinutes(15); await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(), ServiceHarness.Context, Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.RefreshTokens.CountAsync(Ct)));
    }
    [Fact]
    public async Task Unverified_google_email_cannot_create_an_account()
    {
        await using var h = await ServiceHarness.CreateAsync();
        var error = await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(auth => auth.GoogleLoginAsync(Identity() with { EmailVerified = false }, ServiceHarness.Context, Ct)));
        Assert.Equal("GOOGLE_IDENTITY_INVALID", error.Code);
        Assert.Equal(0, await h.DbAsync(db => db.Users.CountAsync(Ct)));
    }
    [Fact]
    public async Task Google_only_user_can_set_password_using_existing_email_reset_flow()
    {
        await using var h = await ServiceHarness.CreateAsync();
        await h.AuthAsync(auth => auth.GoogleLoginAsync(Identity(ServiceHarness.Address), ServiceHarness.Context, Ct));
        Assert.Equal("INVALID_CREDENTIALS", (await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync())).Code);
        await h.AuthAsync(auth => auth.ForgotPasswordAsync(new(ServiceHarness.Address), ServiceHarness.Context, Ct));
        var ticket = await h.AuthAsync(auth => auth.VerifyForgotPasswordOtpAsync(new(ServiceHarness.Address, h.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)]), ServiceHarness.Context, Ct));
        await h.AuthAsync(auth => auth.ResetPasswordAsync(new(ticket.ResetToken, ServiceHarness.Password, ServiceHarness.Password), ServiceHarness.Context, Ct));
        await h.LoginAsync();
    }
}
