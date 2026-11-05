using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class SocialApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<JsonElement> Data(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {text}");
        using var doc = JsonDocument.Parse(text); return doc.RootElement.GetProperty("data").Clone();
    }
    private static async Task<(Guid Id, string Token)> Register(ApiFactory factory, HttpClient client, string name)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var email = name + "@social.example.test";
        await Data(await client.PostAsJsonAsync("/api/auth/register", new { userName = "social." + name, email,
            password = ServiceHarness.Password, confirmPassword = ServiceHarness.Password, firstName = name, lastName = "Social" }, Ct));
        await Data(await client.PostAsJsonAsync("/api/auth/verify-email", new { email, otp = factory.Email.Inbox[(email, OtpPurpose.EmailVerification)] }, Ct));
        var login = await Data(await client.PostAsJsonAsync("/api/auth/login", new { email, password = ServiceHarness.Password }, Ct));
        await using var scope = factory.Services.CreateAsyncScope();
        var id = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync(Ct);
        return (id, login.GetProperty("accessToken").GetString()!);
    }
    private static void As(HttpClient client, string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    private static async Task<string> Relationship(HttpClient client, Guid id) => (await Data(await client.GetAsync($"/api/social/profiles/{id}", Ct))).GetProperty("relationship").GetString()!;

    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task SQL_migrations_and_simultaneous_crossed_requests_create_one_pending_pair()
    {
        await using var factory = new ApiFactory(useSqlServer: true); using var client = await factory.ClientAsync();
        var a = await Register(factory, client, "sqlalice"); var b = await Register(factory, client, "sqlbao");
        using var other = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        As(client, a.Token); As(other, b.Token);
        var replies = await Task.WhenAll(client.PostAsJsonAsync($"/api/social/friends/{b.Id}", new { }, Ct), other.PostAsJsonAsync($"/api/social/friends/{a.Id}", new { }, Ct));
        foreach (var reply in replies) await Data(reply);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = Assert.Single(await db.Friendships.ToArrayAsync(Ct)); Assert.Equal("Pending", link.Status);
        Assert.Single(await db.InAppNotifications.Where(x => x.Kind == "FRIEND_REQUEST").ToArrayAsync(Ct));
        var recipient = link.RequestedById == a.Id ? other : client;
        var requester = link.RequestedById == a.Id ? a.Id : b.Id;
        await Data(await recipient.PostAsJsonAsync($"/api/social/friends/{requester}/accept", new { }, Ct));
        Assert.Equal("Friends", await Relationship(client, b.Id));
        Assert.Equal("Friends", await Relationship(other, a.Id));
        var profile = await Data(await client.GetAsync($"/api/social/profiles/{b.Id}", Ct));
        Assert.Equal(1, profile.GetProperty("friendCount").GetInt32());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
    }

    [Fact] public async Task Friend_requests_are_idempotent_only_recipient_can_accept_and_relationship_is_symmetric()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var a = await Register(factory, client, "alice"); var b = await Register(factory, client, "bao"); var c = await Register(factory, client, "chau");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/social/profiles/{a.Id}", Ct)).StatusCode);
        As(client, a.Token);
        Assert.Equal("Self", await Relationship(client, a.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/social/friends/{a.Id}", new { }, Ct)).StatusCode);
        Assert.Equal("Outgoing", (await Data(await client.PostAsJsonAsync($"/api/social/friends/{b.Id}", new { }, Ct))).GetProperty("relationship").GetString());
        await Data(await client.PostAsJsonAsync($"/api/social/friends/{b.Id}", new { }, Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/social/friends/{b.Id}/accept", new { }, Ct)).StatusCode);
        As(client, c.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/social/friends/{a.Id}/accept", new { }, Ct)).StatusCode);
        As(client, b.Token);
        Assert.Equal("Incoming", await Relationship(client, a.Id));
        Assert.Equal("Incoming", (await Data(await client.PostAsJsonAsync($"/api/social/friends/{a.Id}", new { }, Ct))).GetProperty("relationship").GetString());
        var incoming = await Data(await client.GetAsync("/api/social/connections?kind=incoming", Ct));
        Assert.Single(incoming.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain("email", incoming.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await using (var scope = factory.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.Friendships.CountAsync(Ct));
            Assert.Equal(1, await db.InAppNotifications.CountAsync(x => x.UserId == b.Id && x.Kind == "FRIEND_REQUEST", Ct));
        }
        await Data(await client.PostAsJsonAsync($"/api/social/friends/{a.Id}/accept", new { }, Ct));
        await Data(await client.PostAsJsonAsync($"/api/social/friends/{a.Id}/accept", new { }, Ct));
        Assert.Equal("Friends", await Relationship(client, a.Id));
        var publicProfile = await Data(await client.GetAsync($"/api/social/profiles/{a.Id}", Ct));
        Assert.Equal(1, publicProfile.GetProperty("friendCount").GetInt32());
        foreach (var field in new[] { "email", "tasks", "events", "sessions", "availability", "passwordHash", "securityStamp" }) Assert.False(publicProfile.TryGetProperty(field, out _));
        As(client, a.Token); Assert.Equal("Friends", await Relationship(client, b.Id));
        Assert.Single((await Data(await client.GetAsync($"/api/social/profiles/{b.Id}/friends", Ct))).GetProperty("items").EnumerateArray());
        await Data(await client.DeleteAsync($"/api/social/friends/{b.Id}", Ct));
        Assert.Equal("None", await Relationship(client, b.Id));
        As(client, b.Token); Assert.Equal("None", await Relationship(client, a.Id));
        Assert.Empty((await Data(await client.GetAsync("/api/social/connections?kind=friends", Ct))).GetProperty("items").EnumerateArray());
    }

    [Fact] public async Task Decline_cancel_and_resend_clear_stale_notifications_and_search_omits_inactive_or_unverified_accounts()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var a = await Register(factory, client, "dung"); var b = await Register(factory, client, "emily");
        As(client, a.Token); await Data(await client.PostAsJsonAsync($"/api/social/friends/{b.Id}", new { }, Ct));
        As(client, b.Token); await Data(await client.DeleteAsync($"/api/social/friends/{a.Id}", Ct));
        As(client, a.Token); await Data(await client.PostAsJsonAsync($"/api/social/friends/{b.Id}", new { }, Ct));
        await Data(await client.DeleteAsync($"/api/social/friends/{b.Id}", Ct));
        await Data(await client.DeleteAsync($"/api/social/friends/{b.Id}", Ct));
        Assert.Empty((await Data(await client.GetAsync("/api/social/connections?kind=outgoing", Ct))).GetProperty("items").EnumerateArray());
        await using (var scope = factory.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.InAppNotifications.AnyAsync(x => x.Kind == "FRIEND_REQUEST" && x.IsVisible, Ct));
            db.Users.Add(new() { Id = Guid.NewGuid(), UserName = "hidden", NormalizedUserName = "HIDDEN", Email = "hidden@example.test", NormalizedEmail = "HIDDEN@EXAMPLE.TEST", FirstName = "Invisible", IsActive = false, EmailConfirmed = true });
            db.Users.Add(new() { Id = Guid.NewGuid(), UserName = "unverified", NormalizedUserName = "UNVERIFIED", Email = "unverified@example.test", NormalizedEmail = "UNVERIFIED@EXAMPLE.TEST", FirstName = "Invisible", IsActive = true, EmailConfirmed = false });
            await db.SaveChangesAsync(Ct);
        }
        Assert.Empty((await Data(await client.GetAsync("/api/social/people?search=Invisible", Ct))).GetProperty("items").EnumerateArray());
        var result = await Data(await client.GetAsync("/api/social/people?search=EMILY", Ct));
        Assert.Equal(b.Id, Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.DoesNotContain("email", result.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/social/profiles/{Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/social/connections?kind=other", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/social/people?offset=-1", Ct)).StatusCode);
    }

    [Fact] public async Task Cover_updates_only_self_and_rejects_executable_or_external_content()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var a = await Register(factory, client, "giang"); var b = await Register(factory, client, "hanh"); As(client, a.Token);
        const string png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9WQAAAAASUVORK5CYII=";
        foreach (var url in new[] { "javascript:alert(1)", "https://example.com/cover.jpg", "data:image/svg+xml;base64,PHN2Zy8+", "data:image/png;base64,bm90LWFuLWltYWdl", "data:image/png;base64,!!!" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/social/profile/cover", new { coverUrl = url }, Ct)).StatusCode);
        await Data(await client.PutAsJsonAsync("/api/social/profile/cover", new { coverUrl = png }, Ct));
        As(client, b.Token);
        Assert.Equal(png, (await Data(await client.GetAsync($"/api/social/profiles/{a.Id}", Ct))).GetProperty("coverUrl").GetString());
        Assert.Equal("", (await Data(await client.GetAsync($"/api/social/profiles/{b.Id}", Ct))).GetProperty("coverUrl").GetString());
        As(client, a.Token); await Data(await client.PutAsJsonAsync("/api/social/profile/cover", new { coverUrl = "" }, Ct));
        Assert.Equal("", (await Data(await client.GetAsync($"/api/social/profiles/{a.Id}", Ct))).GetProperty("coverUrl").GetString());
    }

    [Fact] public async Task Public_friend_lists_are_paginated_and_mutual_counts_are_accurate()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var a = await Register(factory, client, "khanh"); var b = await Register(factory, client, "linh"); var c = await Register(factory, client, "mai");
        As(client, c.Token); await Data(await client.PostAsJsonAsync($"/api/social/friends/{a.Id}", new { }, Ct));
        await Data(await client.PostAsJsonAsync($"/api/social/friends/{b.Id}", new { }, Ct));
        As(client, a.Token); await Data(await client.PostAsJsonAsync($"/api/social/friends/{c.Id}/accept", new { }, Ct));
        As(client, b.Token); await Data(await client.PostAsJsonAsync($"/api/social/friends/{c.Id}/accept", new { }, Ct));
        var profile = await Data(await client.GetAsync($"/api/social/profiles/{a.Id}", Ct));
        Assert.Equal(1, profile.GetProperty("mutualFriendCount").GetInt32());
        await using (var scope = factory.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var i = 0; i < 28; i++) db.Users.Add(new() { Id = Guid.NewGuid(), UserName = "person" + i, NormalizedUserName = "PERSON" + i, Email = $"person{i}@example.test", NormalizedEmail = $"PERSON{i}@EXAMPLE.TEST", FirstName = "Person " + i.ToString("D2"), IsActive = true, EmailConfirmed = true });
            await db.SaveChangesAsync(Ct);
        }
        var first = await Data(await client.GetAsync("/api/social/people?search=Person", Ct));
        Assert.Equal(24, first.GetProperty("items").GetArrayLength()); Assert.True(first.GetProperty("hasMore").GetBoolean());
        var second = await Data(await client.GetAsync("/api/social/people?search=Person&offset=24", Ct));
        Assert.Equal(4, second.GetProperty("items").GetArrayLength()); Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.Empty(first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Intersect(second.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())));
    }
}
