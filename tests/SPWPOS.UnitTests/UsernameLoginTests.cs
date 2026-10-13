using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class UsernameLoginTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] public async Task Username_and_email_sign_in_to_the_same_account_case_insensitively()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        Assert.Equal(ServiceHarness.UserName, user.UserName);
        await h.AuthAsync(a => a.LoginAsync(new("  TEST.MEMBER  ", ServiceHarness.Password), ServiceHarness.Context, Ct));
        await h.LoginAsync();
        var sessions = await h.DbAsync(db => db.RefreshTokens.ToArrayAsync(Ct));
        Assert.Equal(2, sessions.Length); Assert.All(sessions, session => Assert.Equal(user.Id, session.UserId));
    }

    [Fact] public async Task Both_aliases_share_the_failed_password_counter_and_lockout()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            var identifier = i % 2 == 0 ? ServiceHarness.UserName : ServiceHarness.Address;
            Assert.Equal("INVALID_CREDENTIALS", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a =>
                a.LoginAsync(new(identifier, "Wrong secret 12!"), ServiceHarness.Context, Ct)))).Code);
        }
        Assert.Equal("ACCOUNT_LOCKED", (await Assert.ThrowsAsync<AuthException>(() => h.LoginAsync())).Code);
        Assert.Equal("ACCOUNT_LOCKED", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a =>
            a.LoginAsync(new(ServiceHarness.UserName, ServiceHarness.Password), ServiceHarness.Context, Ct)))).Code);
    }

    [Fact] public async Task Username_login_still_requires_email_verification()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(false);
        Assert.Equal("EMAIL_NOT_VERIFIED", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a =>
            a.LoginAsync(new(ServiceHarness.UserName, ServiceHarness.Password), ServiceHarness.Context, Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.RefreshTokens.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Duplicate_username_is_rejected_without_creating_an_account_or_sending_OTP()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        const string other = "other@example.com";
        Assert.Equal("USERNAME_TAKEN", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a =>
            a.RegisterAsync(new(other, ServiceHarness.Password, ServiceHarness.Password, "Other", "Member", "TEST.MEMBER"), ServiceHarness.Context, Ct)))).Code);
        Assert.Single(await h.DbAsync(db => db.Users.ToArrayAsync(Ct)));
        Assert.False(h.Email.Inbox.ContainsKey((other, OtpPurpose.EmailVerification)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("member@example.com")]
    [InlineData("two words")]
    [InlineData("têncódấu")]
    [InlineData(".member")]
    public async Task Registration_rejects_invalid_usernames(string username)
    {
        await using var h = await ServiceHarness.CreateAsync();
        Assert.Equal("INVALID_USERNAME", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a =>
            a.RegisterAsync(new(ServiceHarness.Address, ServiceHarness.Password, ServiceHarness.Password, "N", "A", username), ServiceHarness.Context, Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.Users.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Username_is_also_excluded_from_the_registration_password()
    {
        await using var h = await ServiceHarness.CreateAsync();
        Assert.Equal("PASSWORD_POLICY_FAILED", (await Assert.ThrowsAsync<AuthException>(() => h.AuthAsync(a =>
            a.RegisterAsync(new(ServiceHarness.Address, "SafeALPHA.ONE1!", "SafeALPHA.ONE1!", "N", "A", "alpha.one"), ServiceHarness.Context, Ct)))).Code);
    }

    [Fact] public async Task Legacy_accounts_with_email_as_username_still_sign_in_by_email()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        await h.DbAsync(async db => {
            var user = await db.Users.SingleAsync(Ct); user.UserName = user.Email; user.NormalizedUserName = user.NormalizedEmail;
            await db.SaveChangesAsync(Ct);
        });
        await h.LoginAsync();
        Assert.Single(await h.DbAsync(db => db.RefreshTokens.ToArrayAsync(Ct)));
    }
}
