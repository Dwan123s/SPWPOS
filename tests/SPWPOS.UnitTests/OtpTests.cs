using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class OtpTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> RunAsync<T>(ServiceHarness h, Func<IOtpService, Guid, Task<T>> action)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(Ct);
        await using var tx = await AccountTransaction.BeginAsync(db, user.NormalizedEmail!, Ct);
        try
        {
            var result = await action(scope.ServiceProvider.GetRequiredService<IOtpService>(), user.Id);
            await db.SaveChangesAsync(Ct); await tx.CommitAsync(Ct); return result;
        }
        catch (AuthException) { await db.SaveChangesAsync(Ct); await tx.CommitAsync(Ct); throw; }
    }
    private static Task VerifyAsync(ServiceHarness h, string otp, OtpPurpose purpose = OtpPurpose.ForgotPassword) =>
        RunAsync(h, async (s, id) => { await s.VerifyOtpAsync(id, purpose, otp, Ct); return true; });
    private static Task<string> GenerateAsync(ServiceHarness h, OtpPurpose purpose = OtpPurpose.ForgotPassword) =>
        RunAsync(h, (s, id) => s.GenerateOtpAsync(id, purpose, Ct));
    [Fact]
    public async Task Generated_code_is_numeric_hash_only_and_single_use()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        var code = await GenerateAsync(h);
        Assert.Matches("^[0-9]{6}$", code);
        var row = await h.DbAsync(db => db.EmailOtps.SingleAsync(x => x.Purpose == OtpPurpose.ForgotPassword, Ct));
        Assert.Equal(32, row.OtpHash.Length); Assert.Equal(5, row.MaxAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), row.ExpiresAt - row.CreatedAt);
        await VerifyAsync(h, code);
        Assert.Equal("OTP_INVALID", (await Assert.ThrowsAsync<AuthException>(() => VerifyAsync(h, code))).Code);
    }
    [Fact]
    public async Task Five_wrong_attempts_persist_and_invalidate()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        var code = await GenerateAsync(h); string wrong = code == "000000" ? "111111" : "000000";
        for (int i = 0; i < 5; i++)
        {
            var error = await Assert.ThrowsAsync<AuthException>(() => VerifyAsync(h, wrong));
            Assert.Equal(i == 4 ? "OTP_TOO_MANY_ATTEMPTS" : "OTP_INVALID", error.Code);
        }
        Assert.Equal("OTP_TOO_MANY_ATTEMPTS", (await Assert.ThrowsAsync<AuthException>(() => VerifyAsync(h, code))).Code);
        var row = await h.DbAsync(db => db.EmailOtps.SingleAsync(x => x.Purpose == OtpPurpose.ForgotPassword, Ct));
        Assert.Equal(5, row.AttemptCount); Assert.True(row.IsUsed);
    }
    [Fact]
    public async Task Expired_code_is_rejected()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); var code = await GenerateAsync(h);
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("OTP_EXPIRED", (await Assert.ThrowsAsync<AuthException>(() => VerifyAsync(h, code))).Code);
    }
    [Fact]
    public async Task Resend_invalidates_previous_code_and_enforces_cooldown()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); var old = await GenerateAsync(h);
        Assert.Equal("OTP_RESEND_COOLDOWN", (await Assert.ThrowsAsync<AuthException>(() => GenerateAsync(h))).Code);
        h.Clock.Advance(TimeSpan.FromSeconds(61)); var fresh = await GenerateAsync(h);
        var rows = await h.DbAsync(db => db.EmailOtps.Where(x => x.Purpose == OtpPurpose.ForgotPassword).OrderBy(x => x.CreatedAt).ToListAsync(Ct));
        Assert.True(rows[0].IsUsed); Assert.False(rows[1].IsUsed);
        // Random codes may coincidentally match; the previous issuance row is still invalid.
        if (old != fresh) await Assert.ThrowsAsync<AuthException>(() => VerifyAsync(h, old));
        await VerifyAsync(h, fresh);
    }
    [Fact]
    public async Task Purpose_mismatch_is_rejected()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync(); var code = await GenerateAsync(h);
        Assert.Equal("OTP_INVALID", (await Assert.ThrowsAsync<AuthException>(() => VerifyAsync(h, code, OtpPurpose.ChangePassword))).Code);
        await VerifyAsync(h, code);
    }
    [Fact]
    public async Task Sixth_issuance_within_fifteen_minutes_is_rejected()
    {
        await using var h = await ServiceHarness.CreateAsync(); await h.RegisterAsync();
        for (int i = 0; i < 5; i++) { await GenerateAsync(h); h.Clock.Advance(TimeSpan.FromSeconds(61)); }
        Assert.Equal("RATE_LIMITED", (await Assert.ThrowsAsync<AuthException>(() => GenerateAsync(h))).Code);
    }
}
