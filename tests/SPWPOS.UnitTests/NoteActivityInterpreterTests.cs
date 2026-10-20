using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class NoteActivityInterpreterTests
{
    private sealed class Provider(string provider, object[] activities) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            var text = JsonSerializer.Serialize(new { activities, needsReview = false, messages = Array.Empty<string>() });
            object response = provider == "OpenAI"
                ? new { output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } }
                : new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } };
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response)) };
        }
    }
    private static object Activity(string kind, string name, string type, string start, string? end = null, int? duration = null) => new
    {
        kind, name, description = "", type, start, end, estimatedMinutes = duration, deadline = (string?)null,
        priority = 2, isHardDeadline = false, repeatWeeks = 1, question = duration is null && kind == "TASK" ? "Báo cáo cần bao lâu?" : ""
    };
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Gemini")]
    [InlineData("VertexAI")]
    public async Task Mixed_note_keeps_all_activities_and_missing_duration_across_providers(string provider)
    {
        // Regression: school/work/meeting were excluded and the report was moved to a guessed deadline.
        using var handler = new Provider(provider, [
            Activity("EVENT", "Đi học", "CLASS", "2026-10-21T07:00:00+07:00", "2026-10-21T12:00:00+07:00"),
            Activity("EVENT", "Đi làm", "WORK", "2026-10-21T13:00:00+07:00", "2026-10-21T17:00:00+07:00"),
            Activity("EVENT", "Họp đồng nghiệp", "MEETING", "2026-10-21T17:00:00+07:00", "2026-10-21T17:15:00+07:00"),
            Activity("TASK", "Làm lại báo cáo RDS", "WRITING", "2026-10-21T20:30:00+07:00")]);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = provider, ["AiSettings:ApiKey"] = "test-key",
            ["AiSettings:GeminiApiKey"] = "test-key", ["AiSettings:VertexApiKey"] = "test-key", ["AiSettings:VertexAuthentication"] = "ApiKey",
            ["AiSettings:VertexProjectId"] = "test-project", ["AiSettings:VertexLocation"] = "global", ["AiSettings:Model"] = "test-model"
        }).Build();
        var result = await new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance).AnalyzeNoteAsync(
            "thứ 4 đi học 7h–12h, đi làm 13h–17h, họp 15p sau giờ làm, tối từ 20h30 làm báo cáo RDS", true,
            "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-20T12:00:00Z"), TestContext.Current.CancellationToken);
        Assert.Empty(result.Tasks);
        var activities = Assert.IsType<NoteActivity[]>(result.Activities);
        Assert.Equal(new[] { "EVENT", "EVENT", "EVENT", "TASK" }, activities.Select(x => x.Kind));
        Assert.Equal(DateTimeOffset.Parse("2026-10-21T20:30:00+07:00"), activities[3].Start);
        Assert.Null(activities[3].EstimatedMinutes); Assert.Null(activities[3].Deadline);
        Assert.NotEmpty(activities[3].Question);
        using var request = JsonDocument.Parse(handler.RequestBody);
        var schema = provider == "OpenAI" ? request.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema")
            : provider == "VertexAI" ? request.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema")
            : request.RootElement.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("schema");
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), x => x.GetString() == "activities");
    }
    [Fact] public async Task Fixed_events_do_not_require_a_task_estimate_to_reach_review()
    {
        using var handler = new Provider("VertexAI", [Activity("EVENT", "Đi học", "CLASS", "2026-10-21T07:00:00+07:00", "2026-10-21T12:00:00+07:00", 0)]);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "VertexAI", ["AiSettings:VertexApiKey"] = "test-key",
            ["AiSettings:VertexProjectId"] = "test-project", ["AiSettings:Model"] = "test-model"
        }).Build();
        var result = await new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance).AnalyzeNoteAsync(
            "Đi học ngày mai 7h–12h", true, "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-20T12:00:00Z"), TestContext.Current.CancellationToken);
        var activity = Assert.Single(Assert.IsType<NoteActivity[]>(result.Activities));
        Assert.Equal("EVENT", activity.Kind);
        Assert.Null(activity.EstimatedMinutes);
        Assert.Equal(DateTimeOffset.Parse("2026-10-21T07:00:00+07:00"), activity.Start);
    }
    [Theory]
    [InlineData("local-time")]
    [InlineData("no-repeat")]
    [InlineData("nullable-text")]
    [InlineData("incomplete-time")]
    [InlineData("unknown-duration")]
    public async Task Activity_response_variations_preserve_the_activity_for_review(string variant)
    {
        var data = JsonSerializer.SerializeToNode(Activity("EVENT", "Đi học", "CLASS", "2026-10-21T07:00:00+07:00", "2026-10-21T12:00:00+07:00"))!;
        switch (variant)
        {
            case "local-time": data["start"] = "2026-10-21T07:00:00"; data["end"] = "2026-10-21T12:00:00"; break;
            case "no-repeat": data["repeatWeeks"] = 0; break;
            case "nullable-text": data["description"] = null; data["question"] = null; break;
            case "incomplete-time": data["start"] = "not-a-date"; data["end"] = ""; break;
            case "unknown-duration": data["kind"] = "TASK"; data["type"] = "WRITING"; data["estimatedMinutes"] = 0; data["end"] = null; break;
        }
        using var handler = new Provider("VertexAI", [data]); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "VertexAI", ["AiSettings:VertexApiKey"] = "test-key",
            ["AiSettings:VertexProjectId"] = "test-project", ["AiSettings:Model"] = "test-model"
        }).Build();
        var result = await new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance).AnalyzeNoteAsync(
            "Đi học ngày mai 7h–12h", true, "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-20T12:00:00Z"), TestContext.Current.CancellationToken);
        var activity = Assert.Single(Assert.IsType<NoteActivity[]>(result.Activities));
        Assert.Equal("Đi học", activity.Name);
        Assert.Null(activity.EstimatedMinutes);
        Assert.Equal(1, activity.RepeatWeeks);
        if (variant == "incomplete-time") { Assert.Null(activity.Start); Assert.Null(activity.End); Assert.True(result.NeedsReview); }
        else Assert.Equal(DateTimeOffset.Parse("2026-10-21T07:00:00+07:00"), activity.Start);
        if (variant == "unknown-duration") Assert.True(result.NeedsReview);
    }
    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("deadline")]
    public async Task Invalid_task_times_never_become_optional_constraints(string field)
    {
        var data = JsonSerializer.SerializeToNode(Activity("TASK", "Báo cáo", "WRITING", "2026-10-21T20:30:00+07:00", "2026-10-21T21:30:00+07:00", 60))!;
        data["deadline"] = "2026-10-21T22:00:00+07:00";
        data[field] = "invalid";
        using var handler = new Provider("VertexAI", [data]); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "VertexAI", ["AiSettings:VertexApiKey"] = "test-key",
            ["AiSettings:VertexProjectId"] = "test-project", ["AiSettings:Model"] = "test-model"
        }).Build();
        var error = await Assert.ThrowsAsync<AuthException>(() => new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance).AnalyzeNoteAsync(
            "Báo cáo từ 20h30–21h30, hạn chót 22h", true, "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-10-20T12:00:00Z"), TestContext.Current.CancellationToken));
        Assert.Equal("NOTE_AI_UNAVAILABLE", error.Code);
    }
    [Fact] public async Task Mixed_provider_payload_reaches_review_and_saves_all_four_activities_after_duration_is_supplied()
    {
        var ct = TestContext.Current.CancellationToken;
        var day = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        string Local(int minutes) => day.AddMinutes(minutes + 420).ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        using var handler = new Provider("VertexAI", [
            Activity("EVENT", "Đi học", "CLASS", Local(0), Local(300), 0),
            Activity("EVENT", "Đi làm", "WORK", Local(360), Local(600), 0),
            Activity("EVENT", "Họp đồng nghiệp", "MEETING", Local(600), Local(615), 0),
            Activity("TASK", "Báo cáo RDS", "WRITING", Local(810), duration: 0)]);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiSettings:Enabled"] = "true", ["AiSettings:Provider"] = "VertexAI", ["AiSettings:VertexApiKey"] = "test-key",
            ["AiSettings:VertexProjectId"] = "test-project", ["AiSettings:Model"] = "test-model"
        }).Build();
        var interpreter = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        await using var h = await ServiceHarness.CreateAsync(services => services.AddSingleton<ITaskInterpreter>(interpreter));
        var user = await h.RegisterAsync();
        await using var scope = h.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await service.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(d => new AvailabilityInput(d, 0, 1440)).ToArray()), ct);
        var note = await service.SaveNoteAsync(user.Id, null, new("Thời gian biểu", "Đi học, đi làm, họp và báo cáo RDS từ 20h30"), ct);
        note = await service.AnalyzeNoteAsync(user.Id, note.Id, new(note.Version), ct);
        var activities = Assert.IsType<NoteActivity[]>(note.Analysis!.Activities);
        Assert.Equal(4, activities.Length); Assert.Null(activities[3].EstimatedMinutes);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(ct)));
        activities[3] = activities[3] with { EstimatedMinutes = 60 };
        var plan = await service.PreviewNoteActivitiesAsync(user.Id, note.Id, new(note.Version, activities, ReviewedConditions: true), ct);
        var session = Assert.Single(plan.Sessions, x => !x.Preserved);
        Assert.Equal(day.AddMinutes(810), session.Start);
        await service.ApplyNoteActivitiesAsync(user.Id, note.Id, plan.Id, new(note.Version), ct);
        Assert.Equal(3, (await h.DbAsync(db => db.FixedEvents.ToArrayAsync(ct))).Length);
        Assert.Single(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(ct)));
        Assert.Single(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(ct)));
    }
}
