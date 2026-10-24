using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class NotesApiTests
{
    private sealed class AiFactory(bool mixed = false) : ApiFactory
    {
        // Use a fixed local hour so continuous work never straddles midnight.
        private static DateTimeOffset DayStart(DateTimeOffset now) => new(
            now.ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(8), TimeSpan.FromHours(7));
        public NoteAiTestDouble Ai { get; } = new(activities: mixed ? now => [
            new("EVENT", "Đi học", Type: "CLASS", Start: DayStart(now), End: DayStart(now).AddHours(5)),
            new("EVENT", "Đi làm", Type: "WORK", Start: DayStart(now).AddHours(6), End: DayStart(now).AddHours(10)),
            new("EVENT", "Họp", Type: "MEETING", Start: DayStart(now).AddHours(10), End: DayStart(now).AddHours(10).AddMinutes(15)),
            new("TASK", "Báo cáo RDS", Type: "WRITING", Start: DayStart(now).AddHours(13).AddMinutes(30), EstimatedMinutes: 60)
        ] : null);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<ITaskInterpreter>(Ai));
        }
    }
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("data").Clone();
    }

    [Fact] public async Task Mixed_notes_preview_and_confirmation_preserve_events_and_task_times_over_http()
    {
        await using var factory = new AiFactory(true); using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        await Data(await client.PutAsJsonAsync("/api/time/availability", new WeeklyInput(Enumerable.Range(0, 7)
            .Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct));
        var saved = await Data(await client.PostAsJsonAsync("/api/notes", new NoteInput("Mixed note", "Đi học, đi làm, họp và làm báo cáo RDS"), Ct));
        var id = saved.GetProperty("id").GetGuid();
        var analyzed = await Data(await client.PostAsJsonAsync($"/api/notes/{id}/analyze", new NoteAnalysisInput(saved.GetProperty("version").GetGuid()), Ct));
        var activities = JsonSerializer.Deserialize<NoteActivity[]>(analyzed.GetProperty("analysis").GetProperty("activities"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(4, activities.Length);
        var version = analyzed.GetProperty("version").GetGuid();
        var proposal = await Data(await client.PostAsJsonAsync($"/api/notes/{id}/proposals", new NoteActivityPlanInput(version, activities), Ct));
        var before = await Data(await client.GetAsync("/api/workspace", Ct));
        Assert.Equal(0, before.GetProperty("events").GetArrayLength()); Assert.Equal(0, before.GetProperty("tasks").GetArrayLength());
        var planId = proposal.GetProperty("id").GetGuid();
        var result = await Data(await client.PostAsJsonAsync($"/api/notes/{id}/proposals/{planId}/apply", new NoteApplyInput(version), Ct));
        Assert.True(result.GetProperty("plan").GetProperty("isApplied").GetBoolean());
        var after = await Data(await client.GetAsync("/api/workspace", Ct));
        Assert.Equal(3, after.GetProperty("events").GetArrayLength()); Assert.Equal(1, after.GetProperty("tasks").GetArrayLength());
        var report = Assert.Single(after.GetProperty("sessions").EnumerateArray());
        Assert.Equal(activities[3].Start, report.GetProperty("start").GetDateTimeOffset());
        await Data(await client.PostAsJsonAsync($"/api/notes/{id}/proposals/{planId}/apply", new NoteApplyInput(version), Ct));
        var retry = await Data(await client.GetAsync("/api/workspace", Ct));
        Assert.Equal(3, retry.GetProperty("events").GetArrayLength()); Assert.Equal(1, retry.GetProperty("tasks").GetArrayLength());
    }

    [Fact] public async Task Notes_require_authentication_and_validate_content()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notes", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notes/agent", Ct)).StatusCode);
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var capabilities = await Data(await client.GetAsync("/api/notes/agent", Ct));
        Assert.False(capabilities.GetProperty("aiAvailable").GetBoolean());
        Assert.Equal(12, capabilities.GetProperty("maxTasks").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/notes", new NoteInput("", ""), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/notes", new NoteInput("Title", new string('x', 8001)), Ct)).StatusCode);
        var empty = await Data(await client.GetAsync("/api/notes", Ct)); Assert.Equal(0, empty.GetArrayLength());
    }

    [Fact] public async Task Note_endpoints_support_automatic_schedule_and_idempotent_retry()
    {
        await using var factory = new AiFactory(); using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        await Data(await client.PutAsJsonAsync("/api/time/availability",
            new WeeklyInput(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 0, 1440)).ToArray()), Ct));
        var saved = await Data(await client.PostAsJsonAsync("/api/notes", new NoteInput("Lịch tự động", "Viết báo cáo ngày mai 60 phút"), Ct));
        var id = saved.GetProperty("id").GetGuid();
        var input = new NoteScheduleInput(saved.GetProperty("version").GetGuid(), false, 7);
        var scheduled = await Data(await client.PostAsJsonAsync($"/api/notes/{id}/schedule", input, Ct));
        Assert.True(factory.Ai.LastUseAi);
        Assert.Equal("Gemini", scheduled.GetProperty("note").GetProperty("analysis").GetProperty("source").GetString());
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
    [Fact] public async Task Missing_ai_returns_503_instead_of_creating_work_with_rules()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var saved = await Data(await client.PostAsJsonAsync("/api/notes", new NoteInput("AI only", "Viết báo cáo ngày mai 60 phút"), Ct));
        var response = await client.PostAsJsonAsync($"/api/notes/{saved.GetProperty("id").GetGuid()}/analyze", new NoteAnalysisInput(saved.GetProperty("version").GetGuid(), false), Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("NOTE_AI_NOT_CONFIGURED", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        var workspace = await Data(await client.GetAsync("/api/workspace", Ct));
        Assert.Equal(0, workspace.GetProperty("tasks").GetArrayLength()); Assert.Equal(0, workspace.GetProperty("sessions").GetArrayLength());
        var list = await Data(await client.GetAsync("/api/notes", Ct)); Assert.Equal(1, list.GetArrayLength());
    }

    [Fact] public async Task Note_can_be_deleted_via_api()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var saved = await Data(await client.PostAsJsonAsync("/api/notes", new NoteInput("To delete", "Content"), Ct));
        var id = saved.GetProperty("id").GetGuid();
        var version = saved.GetProperty("version").GetGuid();
        var response = await client.DeleteAsync($"/api/notes/{id}?version={version}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
