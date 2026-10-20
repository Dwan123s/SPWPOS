using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Infrastructure.Persistence;
using System.Text.Json;
namespace SPWPOS.IntegrationTests;

public sealed class AgentApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Factory : ApiFactory
    {
        public AgentAiTestDouble Ai { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        { base.ConfigureWebHost(builder); builder.ConfigureServices(s => s.AddSingleton<ITaskInterpreter>(Ai)); }
    }

    [Fact] public async Task A_streamed_overlap_rejection_is_unsaved_and_its_history_replaces_the_provisional_success_text()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var localNow = TimeZoneInfo.ConvertTime(f.Clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        var day = new DateTimeOffset(localNow.Date.AddDays(1).AddHours(9), localNow.Offset);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Đi làm", day, day.AddHours(4), EventType: "WORK"));
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Reply = "Lịch trình sẽ được cập nhật.", Events = [new("Cuộc họp", day.AddHours(1), day.AddHours(2), EventType: "MEETING")] };
        var request = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Thêm cuộc họp ngày mai", true);
        using var response = await client.PostAsJsonAsync("/api/agent/turns/stream", request, Ct);
        var frames = (await response.Content.ReadAsStringAsync(Ct)).Split('\n').Where(x => x.StartsWith("data: ")).Select(x => JsonDocument.Parse(x[6..]).RootElement.Clone()).ToArray();
        var error = Assert.Single(frames, x => x.GetProperty("type").GetString() == "error");
        Assert.Equal("FIXED_EVENT_OVERLAP", error.GetProperty("code").GetString()); Assert.Contains("Đi làm", error.GetProperty("message").GetString());
        Assert.DoesNotContain(frames, x => x.GetProperty("type").GetString() == "result");
        var history = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/turns")).GetProperty("data");
        var turn = Assert.Single(history.EnumerateArray()); Assert.Equal("FAILED", turn.GetProperty("status").GetString());
        Assert.Equal(error.GetProperty("message").GetString(), turn.GetProperty("reply").GetString());
        Assert.DoesNotContain("Lịch trình sẽ được cập nhật", turn.GetProperty("reply").GetString());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal("Đi làm", Assert.Single(workspace.GetProperty("events").EnumerateArray()).GetProperty("name").GetString());
    }
    [Fact] public async Task Agent_routes_require_authentication_and_validate_requests()
    {
        await using var f = new ApiFactory(); using var client = await f.ClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agent/capabilities", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agent/turns", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/agent/turns", new { }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/agent/turns/stream", new { }, Ct)).StatusCode);
        var tokens = await AuthFlowTests.RegisterLoginAsync(f, client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var caps = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/agent/capabilities")).GetProperty("data"); Assert.False(caps.GetProperty("aiAvailable").GetBoolean());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), new string('x', 2001)), HttpStatusCode.BadRequest);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Xếp lịch"), HttpStatusCode.ServiceUnavailable);
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("tasks").EnumerateArray()); Assert.Empty(workspace.GetProperty("events").EnumerateArray());
    }
    [Fact] public async Task Streaming_emits_actual_phases_and_a_final_preview_then_retry_does_not_call_ai_twice()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        await WorkspaceFlowTests.Authorize(f, client); await WorkspaceFlowTests.SetupTime(client);
        var tomorrow = new DateTimeOffset(f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(8), TimeSpan.FromHours(7));
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp", tomorrow, tomorrow.AddHours(1), EventType: "MEETING")] };
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Họp ngày mai", false);
        async Task<System.Text.Json.JsonElement[]> SendStream()
        {
            using var response = await client.PostAsJsonAsync("/api/agent/turns/stream", input, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
            var text = await response.Content.ReadAsStringAsync(Ct);
            return text.Split('\n').Where(line => line.StartsWith("data: ")).Select(line => System.Text.Json.JsonDocument.Parse(line[6..]).RootElement.Clone()).ToArray();
        }
        var messages = await SendStream();
        Assert.Equal(new[] { "READING", "UNDERSTANDING", "PLANNING" }, messages.Where(message => message.GetProperty("type").GetString() == "phase").Select(message => message.GetProperty("stage").GetString()));
        Assert.Contains(messages, message => message.GetProperty("type").GetString() == "delta");
        var turn = messages.Single(message => message.GetProperty("type").GetString() == "result").GetProperty("turn");
        Assert.Equal("PREVIEW", turn.GetProperty("status").GetString());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("events").EnumerateArray());
        var replay = await SendStream();
        Assert.Equal(turn.GetProperty("id").GetGuid(), replay.Last().GetProperty("turn").GetProperty("id").GetGuid());
        Assert.Equal(1, f.Ai.Calls);
    }
    [Fact] public async Task Http_preview_apply_retry_and_undo_follow_the_same_owner_scoped_workflow()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        await WorkspaceFlowTests.Authorize(f, client); await WorkspaceFlowTests.SetupTime(client);
        var tomorrow = new DateTimeOffset(f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(8), TimeSpan.FromHours(7));
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp", tomorrow, tomorrow.AddHours(2), EventType: "MEETING")] };
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Họp ngày mai 8h đến 10h", false);
        var preview = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input)).GetProperty("data");
        Assert.Equal("PREVIEW", preview.GetProperty("status").GetString()); var id = preview.GetProperty("id").GetGuid();
        var applied = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{id}/apply")).GetProperty("data");
        Assert.Equal("APPLIED", applied.GetProperty("status").GetString());
        var retry = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", input)).GetProperty("data"); Assert.Equal(id, retry.GetProperty("id").GetGuid()); Assert.Equal(1, f.Ai.Calls);
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data"); Assert.Single(workspace.GetProperty("events").EnumerateArray());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{id}/undo");
        workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data"); Assert.Empty(workspace.GetProperty("events").EnumerateArray());
    }
    [Fact] public async Task Apply_revalidates_task_work_window_even_if_stored_preview_was_altered()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        await WorkspaceFlowTests.Authorize(f, client); await WorkspaceFlowTests.SetupTime(client);
        var day = new DateTimeOffset(f.Clock.GetUtcNow().UtcDateTime.Date.AddDays(2), TimeSpan.Zero);
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "English", day.AddHours(21), 45, 2, "STUDY", true, day.AddHours(18), day.AddHours(22))] };
        var result = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Schedule English", false))).GetProperty("data");
        var id = result.GetProperty("id").GetGuid(); Assert.Equal("PREVIEW", result.GetProperty("status").GetString());
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var turn = await db.AgentTurns.SingleAsync(x => x.Id == id, Ct);
            var view = JsonSerializer.Deserialize<AgentTurnView>(turn.ResultJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var session = Assert.Single(view.Plan!.Sessions);
            turn.ResultJson = JsonSerializer.Serialize(view with { Plan = view.Plan with { Sessions = [session with { Start = session.Start.AddDays(-1), End = session.End.AddDays(-1) }] } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await db.SaveChangesAsync(Ct);
        }
        var failure = await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{id}/apply", null, HttpStatusCode.Conflict);
        Assert.Equal("TASK_WORK_WINDOW", failure.GetProperty("error").GetProperty("code").GetString());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("tasks").EnumerateArray()); Assert.Empty(workspace.GetProperty("sessions").EnumerateArray());
    }
}
