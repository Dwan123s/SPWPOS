using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamInviteApiTests
{
    [Fact] public async Task Invite_preview_requires_login_and_revoke_invalidates_preview_and_join()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new ApiFactory(); using var client = await f.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(f, client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var created = await client.PostAsJsonAsync("/api/teams", new { name = "SSG105 invite API fixture" }, ct); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var team = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct)); var id = team.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var invitation = await client.PostAsJsonAsync($"/api/teams/{id}/invites", new { }, ct); Assert.Equal(HttpStatusCode.OK, invitation.StatusCode);
        using var body = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync(ct)); var invite = body.RootElement.GetProperty("data");
        var code = invite.GetProperty("code").GetString()!; var inviteId = invite.GetProperty("id").GetGuid();
        var preview = await client.GetAsync($"/api/teams/invites/{code.ToLowerInvariant()}", ct); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var info = JsonDocument.Parse(await preview.Content.ReadAsStringAsync(ct));
        Assert.True(info.RootElement.GetProperty("data").GetProperty("alreadyMember").GetBoolean());
        Assert.Equal("SSG105 invite API fixture", info.RootElement.GetProperty("data").GetProperty("teamName").GetString());
        await using (var scope = f.Services.CreateAsyncScope()) Assert.Equal(20, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TeamInvites.SingleAsync(ct)).RemainingUses);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/teams/invites/{code}", ct)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/teams/{id}/invites/{inviteId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/teams/invites/{code}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/teams/join", new { text = code }, ct)).StatusCode);
    }
}
