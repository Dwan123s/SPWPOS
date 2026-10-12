using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;

namespace SPWPOS.IntegrationTests;

public sealed class FirebaseSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Verifier : IFirebaseTokenVerifier
    {
        public int Calls { get; private set; }
        public string Email { get; set; } = "google@example.com";
        public Task<GoogleIdentity> VerifyGoogleAsync(string idToken, CancellationToken ct)
        {
            Calls++;
            if (idToken != "verified-google-token") throw new AuthException("GOOGLE_IDENTITY_INVALID", "Invalid token", 401);
            return Task.FromResult(new GoogleIdentity("firebase-user-123", Email, true, "Minh", "Nguyễn"));
        }
    }
    private sealed class FirebaseFactory : ApiFactory
    {
        public Verifier Verifier { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:Enabled"] = "true", ["Firebase:ProjectId"] = "planora-test",
                ["Firebase:ApiKey"] = "public-web-api-key", ["Firebase:AppId"] = "public-web-app-id",
                ["Firebase:ServiceAccountPath"] = "server-only-service-account.json"
            }));
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IFirebaseTokenVerifier>(Verifier)));
        }
    }
    private static void BrowserHeaders(HttpClient client, string? origin = "https://localhost")
    {
        client.DefaultRequestHeaders.Add("X-SPWPOS", "1");
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
    }
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    private static Task<HttpResponseMessage> SignIn(HttpClient client, string token = "verified-google-token")
        => client.PostAsJsonAsync("/api/browser-session/firebase/login", new { idToken = token }, Ct);

    [Fact]
    public async Task Verified_google_login_creates_only_secure_browser_cookies_and_normal_session()
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        using var response = await SignIn(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await JsonAsync(response)).GetProperty("data");
        Assert.Single(data.EnumerateObject()); Assert.True(data.TryGetProperty("expiresAt", out _));
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray(); Assert.Equal(2, cookies.Length);
        Assert.Contains(cookies, x => x.StartsWith("__Host-spwpos-access="));
        Assert.Contains(cookies, x => x.StartsWith("__Secure-spwpos-refresh="));
        Assert.All(cookies, cookie => { Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie); });
        using var me = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var config = await client.GetAsync("/api/browser-session/firebase/config", Ct);
        var settings = (await JsonAsync(config)).GetProperty("data");
        Assert.True(settings.GetProperty("linked").GetBoolean()); Assert.False(settings.GetProperty("hasPassword").GetBoolean());
        Assert.Equal("planora-test.firebaseapp.com", settings.GetProperty("config").GetProperty("authDomain").GetString());
    }
    [Theory]
    [InlineData(null)] [InlineData("https://attacker.example")]
    public async Task Missing_or_foreign_origin_is_rejected_before_verification(string? origin)
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client, origin);
        using var response = await SignIn(client);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, factory.Verifier.Calls);
    }
    [Fact]
    public async Task Invalid_token_cannot_create_account_or_cookies()
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        using var response = await SignIn(client, "forged-token"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync(Ct));
    }
    [Theory]
    [InlineData(0)] [InlineData(8193)]
    public async Task Empty_or_oversized_token_is_rejected_before_verification(int length)
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        using var response = await SignIn(client, new string('a', length));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, factory.Verifier.Calls);
    }
    [Fact]
    public async Task Linking_requires_existing_authenticated_session()
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        using var response = await client.PostAsJsonAsync("/api/browser-session/firebase/link", new { idToken = "verified-google-token" }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal(0, factory.Verifier.Calls);
    }
    [Fact]
    public async Task Verified_existing_email_automatically_links_then_reuses_original_account()
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        var local = await AuthFlowTests.RegisterLoginAsync(factory, client); factory.Verifier.Email = ServiceHarness.Address;
        using var automaticLogin = await SignIn(client); Assert.Equal(HttpStatusCode.OK, automaticLogin.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", local.Access);
        using var linked = await client.PostAsJsonAsync("/api/browser-session/firebase/link", new { idToken = "verified-google-token" }, Ct);
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var login = await SignIn(client); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Select(x => x.Id).SingleAsync(Ct);
        Assert.All(await db.RefreshTokens.Select(x => x.UserId).ToListAsync(Ct), id => Assert.Equal(userId, id));
    }
    [Fact]
    public async Task Public_config_and_csp_support_google_without_exposing_private_credentials()
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync();
        using var config = await client.GetAsync("/api/browser-session/firebase/config", Ct);
        Assert.Equal("no-store", config.Headers.CacheControl?.ToString());
        var webConfig = (await JsonAsync(config)).GetProperty("data").GetProperty("config");
        Assert.Equal(4, webConfig.EnumerateObject().Count()); Assert.False(webConfig.TryGetProperty("privateKey", out _));
        Assert.False(webConfig.TryGetProperty("serviceAccountPath", out _));
        using var page = await client.GetAsync("/", Ct);
        var csp = page.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("frame-src https://planora-test.firebaseapp.com", csp);
        Assert.Contains("https://identitytoolkit.googleapis.com", csp); Assert.Contains("https://apis.google.com", csp);
        Assert.DoesNotContain("unsafe-inline", csp); Assert.DoesNotContain("unsafe-eval", csp);
    }
    [Fact]
    public async Task Google_user_sets_password_in_account_security_then_uses_both_methods()
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        factory.Verifier.Email = ServiceHarness.Address;
        using var google = await SignIn(client); Assert.Equal(HttpStatusCode.OK, google.StatusCode);
        using var before = await client.GetAsync("/api/auth/login-methods", Ct);
        Assert.False((await JsonAsync(before)).GetProperty("data").GetProperty("hasPassword").GetBoolean());
        using var requestOtp = await client.PostAsync("/api/auth/change-password/request-otp", null, Ct);
        Assert.Equal(HttpStatusCode.OK, requestOtp.StatusCode);
        var otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)];
        using var mismatch = await client.PostAsJsonAsync("/api/auth/set-password", new { otp, newPassword = ServiceHarness.Password, confirmNewPassword = ServiceHarness.NewPassword }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        using var badPolicy = await client.PostAsJsonAsync("/api/auth/set-password", new { otp, newPassword = "abcdefgh", confirmNewPassword = "abcdefgh" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badPolicy.StatusCode);
        using var setup = await client.PostAsJsonAsync("/api/auth/set-password", new { otp, newPassword = ServiceHarness.Password, confirmNewPassword = ServiceHarness.Password }, Ct);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        using var expired = await client.GetAsync("/api/browser-session/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var password = await client.PostAsJsonAsync("/api/browser-session/login", new { email = ServiceHarness.Address, password = ServiceHarness.Password }, Ct);
        Assert.Equal(HttpStatusCode.OK, password.StatusCode);
        using var after = await client.GetAsync("/api/auth/login-methods", Ct);
        var methods = (await JsonAsync(after)).GetProperty("data");
        Assert.True(methods.GetProperty("hasPassword").GetBoolean()); Assert.True(methods.GetProperty("googleLinked").GetBoolean());
        using var overwrite = await client.PostAsJsonAsync("/api/auth/set-password", new { otp, newPassword = ServiceHarness.NewPassword, confirmNewPassword = ServiceHarness.NewPassword }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, overwrite.StatusCode);
        using var again = await SignIn(client); Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync(Ct));
    }
    [Fact]
    public async Task First_password_and_login_methods_require_authentication()
    {
        await using var factory = new FirebaseFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        using var methods = await client.GetAsync("/api/auth/login-methods", Ct); Assert.Equal(HttpStatusCode.Unauthorized, methods.StatusCode);
        using var setup = await client.PostAsJsonAsync("/api/auth/set-password", new { otp = "123456", newPassword = ServiceHarness.Password, confirmNewPassword = ServiceHarness.Password }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, setup.StatusCode);
    }
    [Fact]
    public async Task Disabled_firebase_does_not_require_server_credentials()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); BrowserHeaders(client);
        using var config = await client.GetAsync("/api/browser-session/firebase/config", Ct);
        var data = (await JsonAsync(config)).GetProperty("data");
        Assert.False(data.GetProperty("enabled").GetBoolean()); Assert.Equal(JsonValueKind.Null, data.GetProperty("config").ValueKind);
        using var response = await SignIn(client); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
