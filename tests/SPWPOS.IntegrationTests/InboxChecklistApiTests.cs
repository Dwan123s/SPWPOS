using System.Net;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.IntegrationTests;

public sealed class InboxChecklistApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Timed_Inbox_text_is_only_a_note_and_the_old_reminder_endpoint_is_removed()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory, client);
        var note = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/notes", new NoteInput("1 phút nữa rep mail khách hàng", "1 phút nữa rep mail khách hàng"))).GetProperty("data");
        var id = note.GetProperty("id").GetGuid();
        Assert.Equal(System.Text.Json.JsonValueKind.Null,note.GetProperty("inboxReminderAt").ValueKind);
        Assert.Empty((await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/notifications")).GetProperty("data").EnumerateArray());
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var due = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/notifications")).GetProperty("data");
        Assert.Empty(due.EnumerateArray());
        using var removed=await client.PostAsync($"/api/notes/{id}/reminder",System.Net.Http.Json.JsonContent.Create(new NoteInboxReminderInput(note.GetProperty("version").GetGuid())),Ct);
        Assert.False(removed.IsSuccessStatusCode);
        await WorkspaceFlowTests.Send(client, HttpMethod.Patch, $"/api/notes/{id}/completion", new NoteInboxCompletionInput(note.GetProperty("version").GetGuid(), true));
        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Empty((await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/notifications")).GetProperty("data").EnumerateArray());
    }
    [Fact]
    public async Task Inbox_completion_is_authenticated_persistent_and_versioned_without_task_creation()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        using var denied = await client.PatchAsync($"/api/notes/{Guid.NewGuid()}/completion", null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await WorkspaceFlowTests.Authorize(factory, client);
        var note = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/notes", new NoteInput("Mua sách", "Mua sách"))).GetProperty("data");
        var id = note.GetProperty("id").GetGuid();
        var version = note.GetProperty("version").GetGuid();
        var completed = (await WorkspaceFlowTests.Send(client, HttpMethod.Patch, $"/api/notes/{id}/completion", new NoteInboxCompletionInput(version, true))).GetProperty("data");
        Assert.True(completed.GetProperty("inboxCompleted").GetBoolean());
        Assert.NotEqual(version, completed.GetProperty("version").GetGuid());
        var list = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/notes")).GetProperty("data");
        Assert.True(Assert.Single(list.EnumerateArray()).GetProperty("inboxCompleted").GetBoolean());
        await WorkspaceFlowTests.Send(client, HttpMethod.Patch, $"/api/notes/{id}/completion", new NoteInboxCompletionInput(version, false), HttpStatusCode.Conflict);
        var reopened = (await WorkspaceFlowTests.Send(client, HttpMethod.Patch, $"/api/notes/{id}/completion", new NoteInboxCompletionInput(completed.GetProperty("version").GetGuid(), false))).GetProperty("data");
        Assert.False(reopened.GetProperty("inboxCompleted").GetBoolean());
        Assert.Empty((await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/tasks")).GetProperty("data").EnumerateArray());
    }
}
