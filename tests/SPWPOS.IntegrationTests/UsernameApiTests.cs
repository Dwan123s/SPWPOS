using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;

namespace SPWPOS.IntegrationTests;

public sealed class UsernameApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] public async Task Register_then_login_by_username_through_bearer_and_browser_endpoints()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        using var registered = await client.PostAsJsonAsync("/api/auth/register", new {
            userName = "username.test", email = ServiceHarness.Address, password = ServiceHarness.Password,
            confirmPassword = ServiceHarness.Password, firstName = "N", lastName = "A"
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        using var verified = await client.PostAsJsonAsync("/api/auth/verify-email", new {
            email = ServiceHarness.Address, otp = factory.Email.Inbox[(ServiceHarness.Address, OtpPurpose.EmailVerification)]
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "USERNAME.TEST", password = ServiceHarness.Password }, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync(Ct));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.RootElement.GetProperty("data").GetProperty("accessToken").GetString());
        using var me = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var profile = JsonDocument.Parse(await me.Content.ReadAsStringAsync(Ct));
        Assert.Equal(ServiceHarness.Address, profile.RootElement.GetProperty("data").GetProperty("email").GetString());
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("X-SPWPOS", "1");
        using var browser = await client.PostAsJsonAsync("/api/browser-session/login", new { email = "username.test", password = ServiceHarness.Password }, Ct);
        Assert.Equal(HttpStatusCode.OK, browser.StatusCode);
        Assert.Contains(browser.Headers.GetValues("Set-Cookie"), value => value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("bad@name")]
    [InlineData("bad name")]
    public async Task Registration_requires_a_valid_username_via_direct_API(string? username)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { userName = username,
            email = ServiceHarness.Address, password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = "N", lastName = "A" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Empty(factory.Email.Inbox);
    }
}
