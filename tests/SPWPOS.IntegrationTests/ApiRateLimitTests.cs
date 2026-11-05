using System.Net;
using SPWPOS.Domain.Enums;
using SPWPOS.TestSupport;

namespace SPWPOS.IntegrationTests;

public sealed class ApiRateLimitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task RegisterBrowserAsync(ApiFactory factory, HttpClient client, string name)
    {
        var email = name + "@example.test";
        client.DefaultRequestHeaders.Add("X-SPWPOS", "1");
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/auth/register", new
        {
            userName = name + "_demo", email, password = ServiceHarness.Password,
            confirmPassword = ServiceHarness.Password, firstName = "Demo", lastName = name
        });
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/auth/verify-email", new
            { email, otp = factory.Email.Inbox[(email, OtpPurpose.EmailVerification)] });
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/browser-session/login", new
            { email, password = ServiceHarness.Password });
    }

    [Fact]
    public async Task Two_accounts_on_one_IP_can_poll_and_act_without_sharing_the_API_budget()
    {
        await using var factory = new ApiFactory();
        using var first = await factory.ClientAsync();
        using var second = await factory.ClientAsync();
        await RegisterBrowserAsync(factory, first, "first");
        await RegisterBrowserAsync(factory, second, "second");
        // A minute of team/chat polling plus user actions exceeds the old shared 60/IP budget.
        for (var n = 0; n < 75; n++)
        {
            using var a = await first.GetAsync("/api/teams", Ct);
            using var b = await second.GetAsync("/api/teams", Ct);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        }
        // Exhaust only the first account. 429 is a response; the host stays available.
        for (var n = 75; n < 240; n++)
        {
            using var response = await first.GetAsync("/api/teams", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await first.GetAsync("/api/teams", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var unaffected = await second.GetAsync("/api/teams", Ct);
        Assert.Equal(HttpStatusCode.OK, unaffected.StatusCode);
        using var publicPage = await first.GetAsync("/swagger/v1/swagger.json", Ct);
        Assert.Equal(HttpStatusCode.OK, publicPage.StatusCode);
    }

    [Fact]
    public async Task Anonymous_API_requests_remain_limited_without_blocking_public_assets()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        for (var n = 0; n < 60; n++)
        {
            using var response = await client.GetAsync("/api/teams", Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var limited = await client.GetAsync("/api/teams", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var publicPage = await client.GetAsync("/swagger/v1/swagger.json", Ct);
        Assert.Equal(HttpStatusCode.OK, publicPage.StatusCode);
    }
}
