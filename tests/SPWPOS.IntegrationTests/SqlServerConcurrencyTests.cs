using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;
// These tests MUST run against SQL Server to verify distributed transaction locks.
// SQLite functional tests deliberately make no concurrency guarantee.
public sealed class SqlServerConcurrencyTests
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<HttpResponseMessage> Post(HttpClient c, string route, object body) => await c.PostAsJsonAsync("/api/auth/" + route, body, Ct);
    private static async Task Register(ApiFactory factory, HttpClient client, bool verify = true)
    {
        using var registered = await Post(client, "register", new { email = ServiceHarness.Address, password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = "N", lastName = "A" });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        if (verify)
        {
            using var verified = await Post(client, "verify-email", new { email = ServiceHarness.Address, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)] });
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        }
    }
    private static async Task<(string Access, string Refresh)> Login(HttpClient client)
    {
        using var response = await Post(client, "login", new { email = ServiceHarness.Address, password = ServiceHarness.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("data");
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Set SPWPOS_TEST_SQLSERVER to run real SQL Server concurrency tests."), Trait("Category", "SqlServer")]
    public async Task Concurrent_email_verification_has_exactly_one_success()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync(); await Register(factory, client, false);
        var body = new { email = ServiceHarness.Address, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)] };
        var responses = await Task.WhenAll(Post(client, "verify-email", body), Post(client, "verify-email", body));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.BadRequest);
            var session = await Login(client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Access);
            using var me = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Set SPWPOS_TEST_SQLSERVER to run real SQL Server concurrency tests."), Trait("Category", "SqlServer")]
    public async Task Concurrent_refresh_rotates_once_and_reuse_revokes_winner_session()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync(); await Register(factory, client);
        var session = await Login(client);
        var body = new { refreshToken = session.Refresh };
        var responses = await Task.WhenAll(Post(client, "refresh-token", body), Post(client, "refresh-token", body));
        try
        {
            var winner = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
            var json = JsonDocument.Parse(await winner.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("data");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
            using var denied = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.RefreshTokens.AllAsync(x => x.RevokedAt != null, Ct));
            Assert.Equal(2, await db.RefreshTokens.CountAsync(Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Set SPWPOS_TEST_SQLSERVER to run real SQL Server concurrency tests."), Trait("Category", "SqlServer")]
    public async Task Concurrent_reset_ticket_is_consumed_once_and_revokes_old_session()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync(); await Register(factory, client); var session = await Login(client);
        using var forgot = await Post(client, "forgot-password", new { email = ServiceHarness.Address }); Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        using var verified = await Post(client, "verify-forgot-password-otp", new { email = ServiceHarness.Address, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)] });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var token = JsonDocument.Parse(await verified.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("data").GetProperty("resetToken").GetString();
        var body = new { resetToken = token, newPassword = ServiceHarness.NewPassword, confirmNewPassword = ServiceHarness.NewPassword };
        var responses = await Task.WhenAll(Post(client, "reset-password", body), Post(client, "reset-password", body));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.BadRequest);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Access);
            using var denied = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            using var login = await Post(client, "login", new { email = ServiceHarness.Address, password = ServiceHarness.NewPassword }); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Set SPWPOS_TEST_SQLSERVER to run real SQL Server concurrency tests."), Trait("Category", "SqlServer")]
    public async Task Concurrent_change_OTP_requests_issue_one_code()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync(); await Register(factory, client);
        var session = await Login(client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Access);
        var responses = await Task.WhenAll(Post(client, "change-password/request-otp", new { }), Post(client, "change-password/request-otp", new { }));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.TooManyRequests);
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.EmailOtps.CountAsync(x => x.Purpose == OtpPurpose.ChangePassword && !x.IsUsed, Ct));
            using var changed = await Post(client, "change-password", new { currentPassword = ServiceHarness.Password, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)], newPassword = ServiceHarness.NewPassword, confirmNewPassword = ServiceHarness.NewPassword });
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Set SPWPOS_TEST_SQLSERVER to run real SQL Server concurrency tests."), Trait("Category", "SqlServer")]
    public async Task Migration_has_unique_normalized_email_and_parallel_register_creates_one_account()
    {
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync();
        var body = new { email = ServiceHarness.Address, password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = "N", lastName = "A" };
        var responses = await Task.WhenAll(Post(client, "register", body), Post(client, "register", body));
        try
        {
            Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.Users.CountAsync(Ct));
            Assert.True(db.Model.FindEntityType(typeof(SPWPOS.Domain.Entities.ApplicationUser))!.GetIndexes().Single(x => x.Properties.Count == 1 && x.Properties[0].Name == "NormalizedEmail").IsUnique);
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync(Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
