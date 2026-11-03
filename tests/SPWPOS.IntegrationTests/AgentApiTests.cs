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

    [Fact] public async Task Calendar_kind_is_bound_validated_and_keeps_events_separate_from_work_sessions_over_HTTP()
    {
        await using var f=new Factory();using var client=await f.ClientAsync();await WorkspaceFlowTests.Authorize(f,client);
        f.Ai.Response=now=>AgentAiTestDouble.Empty() with{Events=[new("Khám nha khoa",now.AddDays(1),now.AddDays(1).AddHours(1),EventType:"APPOINTMENT")]};
        var input=new AgentTurnInput(Guid.NewGuid(),Guid.NewGuid(),"Khám nha khoa ngày mai, giữ đúng khung giờ.",CalendarKind:"WorkSession");
        var mismatch=(await WorkspaceFlowTests.Send(client,HttpMethod.Post,"/api/agent/turns",input)).GetProperty("data");
        Assert.Equal("NEEDS_INPUT",mismatch.GetProperty("status").GetString());
        var empty=(await WorkspaceFlowTests.Send(client,HttpMethod.Get,"/api/workspace")).GetProperty("data");Assert.Empty(empty.GetProperty("events").EnumerateArray());Assert.Empty(empty.GetProperty("tasks").EnumerateArray());
        var saved=(await WorkspaceFlowTests.Send(client,HttpMethod.Post,"/api/agent/turns",input with{RequestId=Guid.NewGuid(),ConversationId=Guid.NewGuid(),CalendarKind="Event"})).GetProperty("data");
        Assert.Equal("APPLIED",saved.GetProperty("status").GetString());
        var after=(await WorkspaceFlowTests.Send(client,HttpMethod.Get,"/api/workspace")).GetProperty("data");Assert.Single(after.GetProperty("events").EnumerateArray());Assert.Empty(after.GetProperty("tasks").EnumerateArray());Assert.Empty(after.GetProperty("sessions").EnumerateArray());
        await WorkspaceFlowTests.Send(client,HttpMethod.Post,"/api/agent/turns",input with{RequestId=Guid.NewGuid(),CalendarKind="Invalid"},HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task New_urgent_meeting_overrides_a_saved_urgent_meeting_and_persists_both_urgency_flags()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var start = new DateTimeOffset(f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(15), TimeSpan.FromHours(7));
        var oldId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events",
            new EventInput("Họp dự án", start, start.AddHours(2), EventType: "MEETING", IsUrgent: true))).GetProperty("data").GetProperty("id").GetGuid();
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp phát sinh", start.AddHours(1), start.AddHours(3), EventType: "MEETING")] };
        var preview = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Ngày mai có cuộc họp phát sinh khẩn từ 16:00 đến 18:00.", true))).GetProperty("data");
        Assert.Equal("PREVIEW", preview.GetProperty("status").GetString());
        Assert.Contains("lịch khẩn cũ", preview.GetProperty("reply").GetString());
        var before = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.True(Assert.Single(before.GetProperty("events").EnumerateArray()).GetProperty("isUrgent").GetBoolean());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{preview.GetProperty("id").GetGuid()}/apply");
        var after = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        var events = after.GetProperty("events").EnumerateArray().ToArray();
        Assert.All(events, item => Assert.True(item.GetProperty("isUrgent").GetBoolean()));
        Assert.Equal(start.AddHours(1), Assert.Single(events, item => item.GetProperty("name").GetString() == "Họp phát sinh").GetProperty("start").GetDateTimeOffset());
        Assert.Equal(start.AddHours(3), Assert.Single(events, item => item.GetProperty("id").GetGuid() == oldId).GetProperty("start").GetDateTimeOffset());
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, $"/api/time/events/{oldId}",
            new EventInput("Họp dự án đã dời", start.AddHours(3), start.AddHours(5), EventType: "MEETING"));
        var edited = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.True(Assert.Single(edited.GetProperty("events").EnumerateArray(), item => item.GetProperty("id").GetGuid() == oldId).GetProperty("isUrgent").GetBoolean());
    }

    [Fact]
    public async Task Urgent_meeting_work_shift_proposal_names_both_events_hours_and_external_permission_over_http()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        await WorkspaceFlowTests.Authorize(f, client);
        var start = new DateTimeOffset(f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(15), TimeSpan.FromHours(7));
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Đi làm", start, start.AddHours(3), EventType: "WORK"));
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Họp khẩn", start.AddHours(1), start.AddHours(3), EventType: "MEETING")] };
        var preview = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Chiều mai 4h tôi có lịch họp khẩn trong 2 tiếng.", true))).GetProperty("data");
        Assert.Equal("PREVIEW", preview.GetProperty("status").GetString());
        var move = Assert.Single(preview.GetProperty("changes").EnumerateArray(), item => item.GetProperty("name").GetString() == "Đi làm");
        Assert.Contains("15:00", move.GetProperty("before").GetString()); Assert.Contains("21:00", move.GetProperty("after").GetString());
        Assert.Contains("được phép đổi ca", move.GetProperty("reason").GetString());
        Assert.Contains("Đi làm", preview.GetProperty("reply").GetString()); Assert.Contains("16:00", preview.GetProperty("reply").GetString());
        var before = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(start, Assert.Single(before.GetProperty("events").EnumerateArray()).GetProperty("start").GetDateTimeOffset());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{preview.GetProperty("id").GetGuid()}/apply");
        var after = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(start.AddHours(3), Assert.Single(after.GetProperty("events").EnumerateArray(), item => item.GetProperty("name").GetString() == "Đi làm").GetProperty("start").GetDateTimeOffset());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Urgent_meeting_displaces_shopping_only_after_confirmation_and_a_stale_preview_cannot_apply(bool stale)
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        await WorkspaceFlowTests.Authorize(f, client);
        var start = new DateTimeOffset(f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(15), TimeSpan.FromHours(7));
        var shoppingId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events",
            new EventInput("Đi mua sắm", start, start.AddHours(1)))).GetProperty("data").GetProperty("id").GetGuid();
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Cuộc họp khẩn", start, start.AddHours(2), EventType: "MEETING")] };
        var request = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Ngày mai tôi có cuộc họp khẩn từ 15:00 đến 17:00, ưu tiên cuộc họp này.", true);
        var turn = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", request)).GetProperty("data");
        var turnId = turn.GetProperty("id").GetGuid();
        Assert.Equal("PREVIEW", turn.GetProperty("status").GetString());
        Assert.True(turn.GetProperty("plan").GetProperty("hasUrgentReplan").GetBoolean());
        var before = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(start, Assert.Single(before.GetProperty("events").EnumerateArray()).GetProperty("start").GetDateTimeOffset());
        if (stale) {
            await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Hẹn khác", start.AddHours(2), start.AddHours(3), EventType: "APPOINTMENT"));
            var rejected = await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{turnId}/apply", expected: HttpStatusCode.Conflict);
            Assert.Equal("AGENT_STALE", rejected.GetProperty("error").GetProperty("code").GetString());
            var unchanged = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
            Assert.Equal(start, Assert.Single(unchanged.GetProperty("events").EnumerateArray(), item => item.GetProperty("id").GetGuid() == shoppingId).GetProperty("start").GetDateTimeOffset());
            Assert.DoesNotContain(unchanged.GetProperty("events").EnumerateArray(), item => item.GetProperty("eventType").GetString() == "MEETING");
            return;
        }
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{turnId}/apply");
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{turnId}/apply");
        var after = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        var events = after.GetProperty("events").EnumerateArray().ToArray(); Assert.Equal(2, events.Length);
        var meeting = Assert.Single(events, item => item.GetProperty("eventType").GetString() == "MEETING");
        Assert.Equal(start, meeting.GetProperty("start").GetDateTimeOffset()); Assert.Equal(start.AddHours(2), meeting.GetProperty("end").GetDateTimeOffset());
        Assert.Equal(start.AddHours(2), Assert.Single(events, item => item.GetProperty("id").GetGuid() == shoppingId).GetProperty("start").GetDateTimeOffset());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/agent/turns/{turnId}/undo");
        var undone = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(start, Assert.Single(undone.GetProperty("events").EnumerateArray()).GetProperty("start").GetDateTimeOffset());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Schedule_review_returns_detailed_advice_and_verified_facts_without_changing_the_calendar(bool streaming)
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var localNow = f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7));
        var start = new DateTimeOffset(localNow.Date.AddDays(1), localNow.Offset);
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/availability", new WeeklyInput([new((int)start.DayOfWeek, 1140, 1320)]));
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Họp dự án", start.AddHours(20), start.AddHours(21)));
        var before = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        const string advice = "Cuộc họp dự án nằm giữa buổi tối, nên thời gian rảnh của bạn được chia thành hai phần riêng. Bạn nên chọn một việc vừa đủ thời lượng cho từng phần, thay vì tính toàn bộ buổi tối là thời gian làm việc liên tục.\n\nMình gợi ý giữ một ít thời gian chuẩn bị trước cuộc họp và rà soát lại việc quan trọng sau đó. Nếu có công việc gần hạn, hãy cho mình biết để mình kiểm tra khung giờ phù hợp trước khi tạo đề xuất cho bạn xác nhận.";
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with { Reply = f.Ai.Calls == 1 ? "Đang kiểm tra lịch." : advice,
            ResultKind = "ANSWER", ScheduleReviewQuery = new(start, start.AddDays(1)) };
        var request = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Phân tích lịch trình ngày mai của tôi.", true);
        JsonElement turn;
        if (streaming) {
            using var response = await client.PostAsJsonAsync("/api/agent/turns/stream", request, Ct);
            var frames = (await response.Content.ReadAsStringAsync(Ct)).Split('\n').Where(x => x.StartsWith("data: "))
                .Select(x => JsonDocument.Parse(x[6..]).RootElement.Clone()).ToArray();
            Assert.Contains(frames, x => x.GetProperty("type").GetString() == "phase" && x.GetProperty("stage").GetString() == "ANALYZING");
            turn = Assert.Single(frames, x => x.GetProperty("type").GetString() == "result").GetProperty("turn");
        } else turn = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", request)).GetProperty("data");
        Assert.Equal("ANSWERED", turn.GetProperty("status").GetString()); Assert.Contains(advice, turn.GetProperty("reply").GetString());
        Assert.Contains("120 phút trống", turn.GetProperty("reply").GetString()); Assert.Empty(turn.GetProperty("changes").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, turn.GetProperty("plan").ValueKind);
        var after = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(before.GetProperty("preference").GetProperty("revision").GetGuid(), after.GetProperty("preference").GetProperty("revision").GetGuid());
        Assert.Single(after.GetProperty("events").EnumerateArray()); Assert.Empty(after.GetProperty("sessions").EnumerateArray());
        Assert.Equal(2, f.Ai.Calls);
        var retry = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns", request)).GetProperty("data");
        Assert.Equal(turn.GetProperty("reply").GetString(), retry.GetProperty("reply").GetString()); Assert.Equal(2, f.Ai.Calls);
    }

    [Fact] public async Task Free_time_stream_and_manual_conflicts_share_the_same_verified_calendar_intervals()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        var localNow = f.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7));
        var start = new DateTimeOffset(localNow.Date.AddDays(1), localNow.Offset);
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/availability",
            new WeeklyInput([new((int)start.DayOfWeek, 1140, 1320)]));
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/exceptions",
            new ExceptionInput(DateOnly.FromDateTime(start.DateTime), 1200, 1260, false));
        f.Ai.Response = _ => AgentAiTestDouble.Empty() with {
            Reply = "Bạn rảnh 19:00–22:00, trừ lúc bận.", ResultKind = "ANSWER", FreeTimeQuery = new(start, start.AddDays(1)) };
        using var response = await client.PostAsJsonAsync("/api/agent/turns/stream",
            new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), "Ngày mai tôi rảnh giờ nào?", true), Ct);
        var frames = (await response.Content.ReadAsStringAsync(Ct)).Split('\n').Where(x => x.StartsWith("data: "))
            .Select(x => JsonDocument.Parse(x[6..]).RootElement.Clone()).ToArray();
        var result = Assert.Single(frames, x => x.GetProperty("type").GetString() == "result").GetProperty("turn");
        Assert.Equal("ANSWERED", result.GetProperty("status").GetString());
        Assert.Contains("19:00–20:00", result.GetProperty("reply").GetString());
        Assert.Contains("21:00–22:00", result.GetProperty("reply").GetString());
        var rejected = await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events",
            new EventInput("Họp", start.AddHours(20), start.AddHours(21)), HttpStatusCode.Conflict);
        Assert.Equal("BUSY_TIME_CONFLICT", rejected.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("21:00–22:00", rejected.GetProperty("error").GetProperty("message").GetString());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/time/events",
            new EventInput("Họp", start.AddHours(21), start.AddHours(22)));
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Single(workspace.GetProperty("events").EnumerateArray());
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
