using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class AuthFlowTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<JsonElement> PostAsync(HttpClient client, string route, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/" + route, body, Ct);
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(expected, response.StatusCode);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
    internal static async Task<(string Access, string Refresh)> RegisterLoginAsync(ApiFactory factory, HttpClient client)
    {
        await PostAsync(client, "register", new { email = ServiceHarness.Address, password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = "Nguyen", lastName = "Van A" });
        await PostAsync(client, "verify-email", new { email = ServiceHarness.Address, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)] });
        return await LoginAsync(client, ServiceHarness.Password);
    }
    private static async Task<(string Access, string Refresh)> LoginAsync(HttpClient client, string password)
    {
        var json = await PostAsync(client, "login", new { email = ServiceHarness.Address, password });
        return (json.GetProperty("data").GetProperty("accessToken").GetString()!, json.GetProperty("data").GetProperty("refreshToken").GetString()!);
    }
    private static void Authorize(HttpClient client, string access) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
    [Theory]
    [InlineData("Abcdef1!")]
    [InlineData("Ab1!aaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Registration_accepts_password_length_boundaries(string password)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await PostAsync(client, "register", new { email = ServiceHarness.Address, password, confirmPassword = password, firstName = "N", lastName = "A" });
        await PostAsync(client, "verify-email", new { email = ServiceHarness.Address, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)] });
        await LoginAsync(client, password);
    }

    [Theory]
    [InlineData("Abcde1!")]
    [InlineData("Ab1!aaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("abcdefgh1!")]
    [InlineData("ABCDEFGH1!")]
    [InlineData("Abcdefgh!")]
    [InlineData("Abcdefgh1")]
    [InlineData("Abcdef1 ")]
    [InlineData("SafePASSWORD1!")]
    [InlineData("SafeWebSiteName1!")]
    [InlineData("SafePlAnOrA1!")]
    [InlineData("SafePERSON1!")]
    public async Task Registration_rejects_invalid_password_via_direct_api(string password)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await PostAsync(client, "register", new { email = ServiceHarness.Address, password, confirmPassword = password, firstName = "N", lastName = "A" }, HttpStatusCode.BadRequest);
        Assert.Empty(factory.Email.Inbox);
    }

    private static async Task<string> ResetTicketAsync(ApiFactory factory, HttpClient client)
    {
        await PostAsync(client, "forgot-password", new { email = ServiceHarness.Address });
        var ticket = await PostAsync(client, "verify-forgot-password-otp", new { email = ServiceHarness.Address, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)] });
        return ticket.GetProperty("data").GetProperty("resetToken").GetString()!;
    }

    [Theory]
    [InlineData("Abcdef1!")]
    [InlineData("Ab1!aaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Reset_accepts_eight_and_thirty_two_character_passwords_via_direct_api(string password)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await RegisterLoginAsync(factory, client);
        string resetToken = await ResetTicketAsync(factory, client);
        await PostAsync(client, "reset-password", new { resetToken, newPassword = password, confirmNewPassword = password });
        await LoginAsync(client, password);
        await PostAsync(client, "login", new { email = ServiceHarness.Address, password = ServiceHarness.Password }, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Abcde1!", "VALIDATION_ERROR")]
    [InlineData("Ab1!aaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "VALIDATION_ERROR")]
    [InlineData("Abcdefgh!", "PASSWORD_POLICY_FAILED")]
    [InlineData("SafePERSON1!", "PASSWORD_POLICY_FAILED")]
    [InlineData("SafePASSWORD1!", "PASSWORD_POLICY_FAILED")]
    [InlineData("SafeWebSiteName1!", "PASSWORD_POLICY_FAILED")]
    [InlineData("SafePlAnOrA1!", "PASSWORD_POLICY_FAILED")]
    [InlineData(ServiceHarness.Password, "PASSWORD_POLICY_FAILED")]
    public async Task Reset_enforces_policy_without_frontend_and_allows_retry_after_rejection(string password, string code)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await RegisterLoginAsync(factory, client);
        string resetToken = await ResetTicketAsync(factory, client);
        var error = await PostAsync(client, "reset-password", new { resetToken, newPassword = password, confirmNewPassword = password }, HttpStatusCode.BadRequest);
        Assert.Equal(code, error.GetProperty("error").GetProperty("code").GetString());
        await LoginAsync(client, ServiceHarness.Password);
        await PostAsync(client, "reset-password", new { resetToken, newPassword = ServiceHarness.NewPassword, confirmNewPassword = ServiceHarness.NewPassword });
        await LoginAsync(client, ServiceHarness.NewPassword);
    }

    [Theory]
    [InlineData("Abcdef1!")]
    [InlineData("Ab1!aaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Change_accepts_eight_and_thirty_two_character_passwords_via_direct_api(string password)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var session = await RegisterLoginAsync(factory, client); Authorize(client, session.Access);
        await PostAsync(client, "change-password/request-otp", new { });
        await PostAsync(client, "change-password", new { currentPassword = ServiceHarness.Password, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)], newPassword = password, confirmNewPassword = password });
        client.DefaultRequestHeaders.Authorization = null;
        await LoginAsync(client, password);
    }
    [Fact]
    public async Task Register_verify_login_and_access_me()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var registered = await PostAsync(client, "register", new { email = ServiceHarness.Address, password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = "Nguyen", lastName = "Van A" });
        Assert.True(registered.GetProperty("success").GetBoolean());
        Assert.DoesNotContain("otp", registered.ToString(), StringComparison.OrdinalIgnoreCase);
        var denied = await PostAsync(client, "login", new { email = ServiceHarness.Address, password = ServiceHarness.Password }, HttpStatusCode.Forbidden);
        Assert.Equal("EMAIL_NOT_VERIFIED", denied.GetProperty("error").GetProperty("code").GetString());
        string otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)];
        await PostAsync(client, "verify-email", new { email = ServiceHarness.Address, otp });
        await PostAsync(client, "verify-email", new { email = ServiceHarness.Address, otp }, HttpStatusCode.BadRequest);
        var session = await LoginAsync(client, ServiceHarness.Password); Authorize(client, session.Access);
        using var response = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct); Assert.Contains(ServiceHarness.Address, body);
        foreach (var forbidden in new[] { "passwordHash", "securityStamp", "concurrencyStamp", "otpHash", "refreshToken" }) Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
    }
    [Fact]
    public async Task Change_password_OTP_invalidates_all_access_and_refresh_sessions()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var first = await RegisterLoginAsync(factory, client); var second = await LoginAsync(client, ServiceHarness.Password);
        Authorize(client, first.Access);
        await PostAsync(client, "change-password/request-otp", new { });
        string otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ChangePassword)];
        await PostAsync(client, "change-password", new { currentPassword = ServiceHarness.Password, otp, newPassword = ServiceHarness.NewPassword, confirmNewPassword = ServiceHarness.NewPassword });
        using var oldAccess = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, oldAccess.StatusCode);
        Authorize(client, second.Access);
        using var otherAccess = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, otherAccess.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        await PostAsync(client, "refresh-token", new { refreshToken = first.Refresh }, HttpStatusCode.Unauthorized);
        await PostAsync(client, "refresh-token", new { refreshToken = second.Refresh }, HttpStatusCode.Unauthorized);
        await PostAsync(client, "login", new { email = ServiceHarness.Address, password = ServiceHarness.Password }, HttpStatusCode.Unauthorized);
        var fresh = await LoginAsync(client, ServiceHarness.NewPassword); Authorize(client, fresh.Access);
        using var current = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal(1, factory.Email.Notifications);
    }
    [Fact]
    public async Task Forgot_OTP_reset_ticket_reset_and_login_with_new_password()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var session = await RegisterLoginAsync(factory, client);
        var known = await PostAsync(client, "forgot-password", new { email = ServiceHarness.Address });
        var absent = await PostAsync(client, "forgot-password", new { email = "absent@example.com" }); Assert.Equal(known.ToString(), absent.ToString());
        string otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.ForgotPassword)];
        var ticketResponse = await PostAsync(client, "verify-forgot-password-otp", new { email = ServiceHarness.Address, otp });
        string resetToken = ticketResponse.GetProperty("data").GetProperty("resetToken").GetString()!;
        await PostAsync(client, "reset-password", new { resetToken, newPassword = ServiceHarness.NewPassword, confirmNewPassword = ServiceHarness.NewPassword });
        await PostAsync(client, "reset-password", new { resetToken, newPassword = ServiceHarness.NewPassword, confirmNewPassword = ServiceHarness.NewPassword }, HttpStatusCode.BadRequest);
        Authorize(client, session.Access);
        using var revoked = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        await PostAsync(client, "refresh-token", new { refreshToken = session.Refresh }, HttpStatusCode.Unauthorized);
        await PostAsync(client, "login", new { email = ServiceHarness.Address, password = ServiceHarness.Password }, HttpStatusCode.Unauthorized);
        await LoginAsync(client, ServiceHarness.NewPassword);
    }
    [Fact]
    public async Task Rotation_reuse_revokes_current_family_and_logout_revokes_other_family()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var first = await RegisterLoginAsync(factory, client); var other = await LoginAsync(client, ServiceHarness.Password);
        var rotated = await PostAsync(client, "refresh-token", new { refreshToken = first.Refresh });
        string refresh = rotated.GetProperty("data").GetProperty("refreshToken").GetString()!;
        await PostAsync(client, "refresh-token", new { refreshToken = first.Refresh }, HttpStatusCode.Unauthorized);
        await PostAsync(client, "refresh-token", new { refreshToken = refresh }, HttpStatusCode.Unauthorized);
        Authorize(client, first.Access);
        using var denied = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Authorize(client, other.Access);
        using var accepted = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await PostAsync(client, "logout", new { });
        using var loggedOut = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, loggedOut.StatusCode);
        await PostAsync(client, "refresh-token", new { refreshToken = other.Refresh }, HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task Logout_all_revokes_every_device()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var first = await RegisterLoginAsync(factory, client); var other = await LoginAsync(client, ServiceHarness.Password); Authorize(client, first.Access);
        await PostAsync(client, "logout-all", new { });
        Authorize(client, other.Access); using var response = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Theory]
    [InlineData("register", "{\"email\":\"bad\",\"password\":\"short\"}")]
    [InlineData("verify-email", "{\"email\":\"person@example.com\",\"otp\":\"abc123\"}")]
    [InlineData("reset-password", "{\"resetToken\":\"short\",\"newPassword\":\"short\"}")]
    [InlineData("login", "{}")]
    public async Task Invalid_inputs_return_ProblemDetails_without_reflecting_values(string route, string body)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/auth/" + route, content, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        Assert.False(json.GetProperty("success").GetBoolean()); Assert.Equal("VALIDATION_ERROR", json.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("short", json.ToString());
    }
    [Fact]
    public async Task Protected_endpoints_require_authentication_and_HTTPS()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        using var denied = await client.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var plain = await client.GetAsync("http://localhost/api/auth/me", Ct); Assert.Equal(HttpStatusCode.BadRequest, plain.StatusCode);
        using var tooLarge = await client.PostAsync("/api/auth/login", new StringContent(new string('x', 3 * 1024 * 1024 + 1000), Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }
    [Fact]
    public async Task Account_and_IP_rate_limits_return_429()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        for (int i = 0; i < 10; i++) await PostAsync(client, "login", new { email = "absent@example.com", password = "wrong password" }, HttpStatusCode.Unauthorized);
        var error = await PostAsync(client, "login", new { email = "absent@example.com", password = "wrong password" }, HttpStatusCode.TooManyRequests);
        Assert.Equal("RATE_LIMITED", error.GetProperty("error").GetProperty("code").GetString());
        // A separate server has fresh counters. Protected requests still pass through IP limiting.
        await using var other = new ApiFactory(); using var ipClient = await other.ClientAsync();
        for (int i = 0; i < 20; i++) { using var result = await ipClient.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode); }
        using var blocked = await ipClient.GetAsync("/api/auth/me", Ct); Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }
    [Fact]
    public async Task Swagger_contains_all_routes_and_Bearer_security()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        var paths = json.GetProperty("paths");
        Assert.Equal(13, paths.EnumerateObject().Count(x => x.Name.StartsWith("/api/auth/", StringComparison.Ordinal)));
        Assert.True(paths.TryGetProperty("/api/tasks", out _));
        Assert.True(paths.TryGetProperty("/api/plans", out _));
        Assert.True(paths.TryGetProperty("/api/workspace", out _));
        Assert.Equal("bearer", json.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
    }
    [Fact]
    public async Task Database_audit_does_not_contain_submitted_security_secrets()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var session = await RegisterLoginAsync(factory, client);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await db.SecurityAuditLogs.ToListAsync(Ct); Assert.NotEmpty(logs);
        string serialized = JsonSerializer.Serialize(logs);
        foreach (string secret in new[] { ServiceHarness.Password, session.Access, session.Refresh, factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)] }) Assert.DoesNotContain(secret, serialized);
    }
}
