using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class NotesApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("data").Clone();
    }

    [Fact] public async Task Notes_require_authentication_and_validate_content()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notes", Ct)).StatusCode);
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/notes", new NoteInput("", ""), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/notes", new NoteInput("Title", new string('x', 8001)), Ct)).StatusCode);
        var empty = await Data(await client.GetAsync("/api/notes", Ct)); Assert.Equal(0, empty.GetArrayLength());
    }

    [Fact] public async Task Note_endpoints_support_automatic_schedule_and_idempotent_retry()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        await Data(await client.PutAsJsonAsync("/api/time/availability",
            new WeeklyInput(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct));
        var saved = await Data(await client.PostAsJsonAsync("/api/notes", new NoteInput("Lịch tự động", "Viết báo cáo ngày mai 60 phút"), Ct));
        var id = saved.GetProperty("id").GetGuid();
        var input = new NoteScheduleInput(saved.GetProperty("version").GetGuid(), false, 7);
        var scheduled = await Data(await client.PostAsJsonAsync($"/api/notes/{id}/schedule", input, Ct));
        Assert.True(scheduled.GetProperty("plan").GetProperty("isApplied").GetBoolean());
        var retry = await Data(await client.PostAsJsonAsync($"/api/notes/{id}/schedule", input, Ct));
        Assert.Equal(scheduled.GetProperty("plan").GetProperty("id").GetGuid(), retry.GetProperty("plan").GetProperty("id").GetGuid());
        var workspace = await Data(await client.GetAsync("/api/workspace", Ct));
        Assert.Equal(1, workspace.GetProperty("tasks").GetArrayLength());
        Assert.True(workspace.GetProperty("sessions").GetArrayLength() > 0);
        var list = await Data(await client.GetAsync("/api/notes", Ct)); Assert.Equal(1, list.GetArrayLength());
        var missing = await client.PostAsJsonAsync($"/api/notes/{Guid.NewGuid()}/analyze", new NoteAnalysisInput(Guid.NewGuid()), Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
