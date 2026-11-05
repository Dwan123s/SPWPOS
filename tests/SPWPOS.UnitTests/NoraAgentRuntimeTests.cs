using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed partial class NoraAgentRuntimeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Goal = "Làm slide 90 phút liên tục, hạn chót 12:00 ngày 01/11/2026, giữ nguyên lịch học.";
    private static JsonElement Data(object value) => JsonSerializer.SerializeToElement(value, Json);
    private static NoraModelReply Call(string name, object args, int number) => new(Data(new { role = "model", parts = new[] { new { functionCall = new { name, args } } } }),
        [new(number.ToString(), name, Data(args))]);
    private const string Draft = """
        {"reply":"Mình sẽ thử xếp buổi làm slide và giữ nguyên lịch học.","resultKind":"MUTATION","question":"","options":[],
        "freeTimeQuery":null,"scheduleReviewQuery":null,"optimizationQuery":null,"replaceWeeklyAvailability":false,"weeklySlots":[],"exceptions":[],"events":[],
        "tasks":[{"taskId":null,"name":"Làm slide","deadline":"2026-11-01T12:00:00Z","estimatedMinutes":90,"priority":2,"taskType":"WRITING","splittable":false,
        "workWindowStart":null,"workWindowEnd":null,"durationEvidence":"90 phút","deadlineEvidence":"hạn chót 12:00 ngày 01/11/2026","workWindowEvidence":""}],
        "scheduleTaskIds":[],"reschedule":false,"horizonDays":2}
        """;
    private sealed class Model : ITaskInterpreter, IPlanningAgentInterpreter, INoraAgentModel
    {
        public NoteAgentCapabilities Capabilities => new(true, Provider: "Gemini");
        public bool AgentAvailable => true;
        public bool SupportsRequestUnderstanding { get; set; }
        public int Calls { get; private set; }
        public string DraftText { get; set; } = Draft;
        public Func<NoraModelRequest, int, Task<NoraModelReply>>? Decision { get; set; }
        public async Task<NoraModelReply> NextAgentStepAsync(NoraModelRequest request, CancellationToken ct)
        {
            Calls++;
            if (Decision is not null) return await Decision(request, Calls);
            if (request.Contents.Length == 1) return Call("prepare_changes", new { intent = JsonDocument.Parse(DraftText).RootElement }, Calls);
            var response = request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse");
            var observation = response.GetProperty("response");
            if (observation.GetProperty("status").GetString() == "ERROR") throw new InvalidOperationException(observation.GetRawText());
            var data = observation.GetProperty("data");
            return response.GetProperty("name").GetString() switch {
                "prepare_changes" => Call("simulate_plan", new { draftId = data.GetProperty("draftId").GetString(), strategy = "keep_existing" }, Calls),
                "simulate_plan" when data.GetProperty("feasible").GetBoolean() => Call("finish_proposal", new { candidateId = data.GetProperty("candidateId").GetString(), reply = "Mình đã tìm được chỗ cho 90 phút làm slide, giữ nguyên lịch học. Bạn xem phương án nhé." }, Calls),
                "simulate_plan" => Call("ask_user", new { reply = "Chưa đủ chỗ cho 90 phút liên tục.", question = "Bạn có muốn thêm giờ rảnh không?", options = Array.Empty<string>() }, Calls),
                _ => throw new InvalidOperationException("Unexpected observation")
            };
        }
        public Task<AgentIntent> InterpretAsync(string text, string contextJson, AgentMessage[] history, string timeZoneId, DateTimeOffset now, CancellationToken ct) => throw new InvalidOperationException("Legacy workflow must not run in Agent mode");
        public Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string zone, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<TaskSuggestion> CaptureAsync(CaptureInput input, string zone, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<BreakdownView> BreakdownAsync(TaskInput input, bool useAi, CancellationToken ct) => throw new NotSupportedException();
    }
    private static async Task<ServiceHarness> Harness(Model model, int rounds = 8, string? fileLogRoot = null)
    {
        var h = await ServiceHarness.CreateAsync(s => { s.AddSingleton<ITaskInterpreter>(model);
            if (fileLogRoot is not null) s.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new FileLogTestEnvironment("Development", fileLogRoot));
            s.Configure<NoraAgentOptions>(x => { x.Mode = "Agent"; x.MaxModelTurns = rounds; }); });
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-31T08:00:00Z") - h.Clock.GetUtcNow());
        return h;
    }
    private static async Task<T> Agent<T>(ServiceHarness h, Func<IPlanningAgentService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IPlanningAgentService>()); }
    private static async Task Setup(ServiceHarness h, Guid uid)
    {
        await using var scope = h.Provider.CreateAsyncScope(); var work = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await work.SetPreferenceAsync(uid, new("UTC", 25, 240, 0, 480, 1320, MaximumDailyMinutes: 480), Ct);
        await work.SetAvailabilityAsync(uid, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 480, 1320)).ToArray()), Ct);
    }

    [Fact] public async Task Native_agent_receives_both_original_timetable_images_together()
    {
        var image = new AgentImage("Lịch học.png", "image/png", "iVBORw0KGgo=", AdditionalImages:
            [new("Lịch làm.jpg", "image/jpeg", "/9j/AA==")]);
        var model = new Model { Decision = (request, number) => {
            var parts = request.Contents[0].GetProperty("parts");
            Assert.Equal(3, parts.GetArrayLength());
            Assert.Equal(image.Data, parts[1].GetProperty("inlineData").GetProperty("data").GetString());
            Assert.Equal(image.AdditionalImages![0].Data, parts[2].GetProperty("inlineData").GetProperty("data").GetString());
            Assert.Contains("read BOTH", request.Instructions);
            return Task.FromResult(Call("ask_user", new { reply = "Đã xem cả hai ảnh.", question = "Bạn áp dụng từ ngày nào?", options = Array.Empty<string>() }, number));
        } };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), "Gộp hai thời khóa biểu và tìm giờ rảnh", Image: image), Ct));
        Assert.Equal("NEEDS_INPUT", turn.Status);
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(Ct)));
    }

    [Fact] public async Task Timetable_agent_saves_fixed_hours_and_proposes_a_routine_for_review()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Draft)!;
        node["tasks"] = new System.Text.Json.Nodes.JsonArray();
        node["events"] = System.Text.Json.Nodes.JsonNode.Parse("""
            [{"name":"Toán","start":"2026-11-01T08:00:00+07:00","end":"2026-11-01T10:00:00+07:00",
            "description":"","eventType":"CLASS","repeatWeeks":1,"existingId":null}]
            """);
        var model = new Model { DraftText = node.ToJsonString() };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), "Nhập thời gian biểu ngày mai: Toán 08:00–10:00, chỉ một lần."), Ct));
        Assert.Equal("APPLIED", turn.Status); Assert.Equal("APPLIED", turn.Run!.Status);
        Assert.DoesNotContain("mặc định", turn.Reply);
        Assert.Contains("Giờ rảnh đề xuất:", turn.Reply); Assert.True(turn.SuggestRoutine);
        Assert.Contains("bữa trưa/tối 30 phút", turn.Reply);
        Assert.Equal(0, await h.DbAsync(db => db.AvailabilityPolicies.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WeeklyAvailability.CountAsync(Ct)));
        await Agent(h, s => s.UndoAgentTurnAsync(user.Id, turn.Id, Ct));
        Assert.Empty(await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Combined_proposal_needs_one_model_call_and_preserves_preview_and_validation()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("propose_changes", new { intent = JsonDocument.Parse(Draft).RootElement, strategy = "keep_existing" }, n)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal, false), Ct));
        Assert.Equal("PREVIEW", turn.Status); Assert.Equal(1, model.Calls); Assert.Equal(1, turn.Run!.ModelTurns);
        Assert.Equal("propose_changes", Assert.Single(turn.Run.Steps).Tool);
        Assert.Equal(90, (Assert.Single(turn.Plan!.Sessions).End - turn.Plan.Sessions[0].Start).TotalMinutes);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct))); Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Combined_proposal_cannot_bypass_a_read_only_request_or_invent_available_hours()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("propose_changes", new { intent = JsonDocument.Parse(Draft).RootElement, strategy = "keep_existing" }, n)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var read = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Chỉ xem, không tạo công việc. " + Goal), Ct));
        Assert.Equal("BLOCKED", read.Status);
        var blocked = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal), Ct));
        Assert.Equal("BLOCKED", blocked.Status);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct))); Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Theory] [InlineData("answer_calendar")] [InlineData("answer_free_time")]
    public async Task Read_only_answer_uses_one_call_and_verified_server_facts(string tool)
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call(tool, new { start = "2026-11-01T00:00:00Z", end = "2026-11-02T00:00:00Z" }, n)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => { db.FixedEvents.Add(new() { UserId = user.Id, Name = "PRIVATE_CLASS", Start = DateTimeOffset.Parse("2026-11-01T08:00:00Z"), End = DateTimeOffset.Parse("2026-11-01T10:00:00Z") }); await db.SaveChangesAsync(Ct); });
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), tool == "answer_calendar" ? "Xem lịch ngày mai" : "Tìm giờ rảnh ngày mai"), Ct));
        Assert.Equal("ANSWERED", turn.Status); Assert.Equal(1, model.Calls); Assert.NotEmpty(turn.Reply);
        Assert.Equal(tool, Assert.Single(turn.Run!.Steps).Tool); Assert.Empty(turn.Changes);
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Native_agent_estimate_stays_pending_until_the_user_accepts_it_in_chat()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("ask_user", new {
            reply = "Mình đề xuất ước lượng 90 phút cho Làm slide, dựa trên yêu cầu trình bày.",
            question = "Bạn đồng ý với ước lượng này không?", options = new[] { "Đồng ý", "Đổi thời lượng" }
        }, n)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var conversation = Guid.NewGuid();
        var proposed = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Xếp Làm slide, hạn chót 12:00 ngày 01/11/2026. Tôi chưa biết cần bao lâu."), Ct));
        Assert.Equal("NEEDS_INPUT", proposed.Status);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        model.Decision = null;
        var scheduled = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Đồng ý"), Ct));
        Assert.Equal("APPLIED", scheduled.Status);
        Assert.Equal(90, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).EstimatedMinutes);
    }

    [Fact] public async Task File_log_tracks_real_runtime_model_rounds_and_tool_results_without_prompt_text()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "file-log-tests", Guid.NewGuid().ToString("N"));
        await using var h = await Harness(new Model(), fileLogRoot: root);
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal, false), Ct));
        Assert.Equal("PREVIEW", turn.Status);
        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(root, "logs", "nora-agent"))));
        Assert.DoesNotContain(Goal, text);
        var entries = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
        Assert.Equal(3, entries.Count(x => x.GetProperty("eventName").GetString() == "MODEL_COMPLETED"));
        Assert.Equal(new[] { "prepare_changes", "simulate_plan", "finish_proposal" }, entries
            .Where(x => x.GetProperty("eventName").GetString() == "TOOL_COMPLETED")
            .Select(x => x.GetProperty("details").GetProperty("tool").GetString()));
        Assert.Equal("RUN_COMPLETED", entries[^1].GetProperty("eventName").GetString());
        Assert.All(entries, x => Assert.Equal(turn.Run!.Id.ToString(), x.GetProperty("runId").GetString()));
    }

    [Fact] public async Task Simulation_is_a_draft_then_exact_candidate_can_be_applied_retried_and_undone()
    {
        var model = new Model(); await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var fixedId = Guid.NewGuid();
        await h.DbAsync(async db => { db.FixedEvents.Add(new() { Id = fixedId, UserId = user.Id, Name = "Lịch học", EventType = "CLASS", Start = h.Clock.GetUtcNow().AddHours(1), End = h.Clock.GetUtcNow().AddHours(3) }); await db.SaveChangesAsync(Ct); });
        var events = new List<AgentStreamEvent>(); var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(), Goal, false);
        var preview = await Agent(h, s => s.SendAgentTurnStreamingAsync(user.Id, input, (item, _) => { events.Add(item); return Task.CompletedTask; }, Ct));
        Assert.Equal("PREVIEW", preview.Status); Assert.Equal("Agent", preview.Run!.Mode); Assert.Equal(3, model.Calls);
        Assert.Equal(new[] { "prepare_changes", "simulate_plan", "finish_proposal" }, preview.Run.Steps.Select(x => x.Tool));
        Assert.Equal(3, events.Count(x => x.Type == "tool"));
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
        var expected = Assert.Single(preview.Plan!.Sessions, x => !x.Preserved);
        Assert.Equal(90, (expected.End - expected.Start).TotalMinutes);
        var retry = await Agent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct)); Assert.Equal(preview.Id, retry.Id); Assert.Equal(3, model.Calls);
        var applied = await Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal("APPLIED", applied.Run!.Status);
        var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct)); Assert.Equal(expected.TaskId, task.Id);
        var session = await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct)); Assert.Equal(expected.Id, session.Id); Assert.Equal(expected.Start, session.Start);
        Assert.Equal(fixedId, (await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct))).Id);
        var undone = await Agent(h, s => s.UndoAgentTurnAsync(user.Id, preview.Id, Ct)); Assert.Equal("UNDONE", undone.Run!.Status);
        Assert.Equal("UNDONE", (await h.DbAsync(db => db.AgentRuns.SingleAsync(Ct))).Status);
        Assert.DoesNotContain("thoughtSignature", await h.DbAsync(db => db.AgentRuns.Select(x => x.StateJson).SingleAsync(Ct)));
    }

    [Fact] public async Task Infeasible_observation_causes_question_without_saving_partial_work()
    {
        var model = new Model(); await using var h = await Harness(model); var user = await h.RegisterAsync();
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Equal("WAITING_FOR_INPUT", result.Run!.Status);
        Assert.Equal("ask_user", result.Run.Steps[^1].Tool);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Fact] public async Task Fake_candidate_is_rejected_and_cannot_autosave()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("finish_proposal", new { candidateId = Guid.NewGuid().ToString(), reply = "Đã lưu" }, n)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal), Ct));
        Assert.Equal("BLOCKED", result.Status); Assert.Equal("REPEATED_TOOL_ERRORS", result.ErrorCode);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.All(await h.DbAsync(db => db.AgentToolCalls.ToArrayAsync(Ct)), x => Assert.Equal("CANDIDATE_NOT_FOUND", x.ResultCode));
    }

    [Fact] public async Task Asking_again_resumes_same_goal_and_keeps_counters()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("ask_user", new { reply = "Mình cần thêm thời lượng.", question = "Bạn cần bao nhiêu phút?", options = Array.Empty<string>() }, n)) };
        await using var h = await Harness(model, 2); var user = await h.RegisterAsync(); var conversation = Guid.NewGuid();
        var a = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Xếp lịch làm slide"), Ct));
        var b = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "90 phút"), Ct));
        Assert.Equal(a.Run!.Id, b.Run!.Id); Assert.Equal(2, b.Run.ModelTurns); Assert.Equal(2, b.Run.ToolCalls);
        var c = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Trước trưa mai"), Ct));
        Assert.Equal("NEEDS_INPUT", c.Status); Assert.Equal(3, model.Calls); Assert.Equal(a.Run.Id, c.Run!.Id);
        Assert.Equal(3, c.Run.ModelTurns); Assert.Equal(3, c.Run.ToolCalls);
        Assert.Equal(1, await h.DbAsync(db => db.AgentRuns.CountAsync(Ct)));
    }

    [Fact] public async Task A_processing_budget_stop_can_resume_the_original_goal_without_losing_pending_context()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("get_preferences", new { }, n)) };
        await using var h = await Harness(model, 3); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var conversation = Guid.NewGuid();
        var stopped = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), Goal), Ct));
        Assert.Equal("BLOCKED", stopped.Status); Assert.Equal("AGENT_BUDGET_EXCEEDED", stopped.ErrorCode);
        Assert.DoesNotContain("thu hẹp", stopped.Reply);
        model.Decision = null;
        var completed = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Tiếp tục xử lý"), Ct));
        Assert.Equal("APPLIED", completed.Status); Assert.Equal(stopped.Run!.Id, completed.Run!.Id);
        Assert.Equal(6, completed.Run.ModelTurns); Assert.Single(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        var run = await h.DbAsync(db => db.AgentRuns.SingleAsync(Ct));
        Assert.Contains(Goal, run.Goal); Assert.Contains("Tiếp tục xử lý", run.Goal);
    }

    [Fact] public async Task A_clarification_reply_gets_a_fresh_simulation_allowance_without_resetting_audit_counts()
    {
        var model = new Model(); await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        h.Provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NoraAgentOptions>>().Value.MaxSimulations = 1;
        model.Decision = (request, n) => Task.FromResult(request.Contents.Length == 1
            ? Call("prepare_changes", new { intent = JsonDocument.Parse(Draft).RootElement }, n)
            : request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("name").GetString() == "prepare_changes"
                ? Call("simulate_plan", new { draftId = request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("response").GetProperty("data").GetProperty("draftId").GetString(), strategy = "keep_existing" }, n)
                : Call("ask_user", new { reply = "Đã kiểm tra phương án, cần xác nhận phạm vi.", question = "Bạn giữ nguyên lịch học phải không?", options = new[] { "Giữ nguyên" } }, n));
        var conversation = Guid.NewGuid();
        var first = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), Goal), Ct));
        Assert.Equal("NEEDS_INPUT", first.Status);
        model.Decision = null;
        var second = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Giữ nguyên lịch học"), Ct));
        Assert.Equal("APPLIED", second.Status);
        Assert.Equal(2, (await h.DbAsync(db => db.AgentRuns.SingleAsync(Ct))).Simulations);
    }

    [Fact] public async Task Timetable_week_confirmation_can_finish_after_cumulative_calls_exceed_eight()
    {
        var payload = System.Text.Json.Nodes.JsonNode.Parse(Draft)!;
        payload["tasks"] = new System.Text.Json.Nodes.JsonArray(); payload["horizonDays"] = 7;
        payload["events"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(
            new[] { 2, 3, 4, 6 }.SelectMany(day => new[] { 0, 1 }.Select(slot => new {
                name = $"Môn {day}-{slot}", start = $"2026-11-{day:00}T{(slot == 0 ? "07:00" : "09:30")}:00+07:00",
                end = $"2026-11-{day:00}T{(slot == 0 ? "09:15" : "11:45")}:00+07:00",
                description = "", eventType = "CLASS", repeatWeeks = 1, existingId = (string?)null })), Json));
        var model = new Model { DraftText = payload.ToJsonString() };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-01T06:54:00Z") - h.Clock.GetUtcNow());
        var conversation = Guid.NewGuid();
        var initialRounds = 0;
        model.Decision = (_, n) => Task.FromResult(++initialRounds < 4 ? Call("get_preferences", new { }, n) : Call("ask_user", new {
            reply = "Lịch trong ảnh thuộc tuần trước.", question = "Bạn muốn áp dụng vào tuần nào?",
            options = new[] { "Áp dụng cho tuần tới (02/11 - 08/11)" } }, n));
        var question = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Nhập thời khóa biểu trong ảnh."), Ct));
        Assert.Equal("NEEDS_INPUT", question.Status);
        var followupRound = 0;
        model.Decision = (request, n) => {
            followupRound++;
            return followupRound switch {
                1 or 2 => Task.FromResult(Call("get_preferences", new { }, n)),
                3 => Task.FromResult(Call("prepare_changes", new { intent = JsonDocument.Parse(model.DraftText).RootElement }, n)),
                4 => Task.FromResult(Call("simulate_plan", new { draftId = request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("response").GetProperty("data").GetProperty("draftId").GetString(), strategy = "keep_existing" }, n)),
                _ => Task.FromResult(Call("finish_proposal", new { candidateId = request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("response").GetProperty("data").GetProperty("candidateId").GetString(), reply = "Đã đối chiếu đầy đủ tám buổi học cho tuần bạn chọn." }, n))
            };
        };
        var saved = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(conversation, Guid.NewGuid(), "Áp dụng cho tuần tới (02/11 - 08/11)"), Ct));
        Assert.Equal("APPLIED", saved.Status); Assert.Equal(question.Run!.Id, saved.Run!.Id);
        Assert.Equal(9, saved.Run.ModelTurns);
        Assert.Equal(8, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WeeklyAvailability.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Read_only_request_cannot_be_converted_into_a_mutating_draft()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("prepare_changes", new { intent = JsonDocument.Parse(Draft).RootElement }, n)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Chỉ xem, không tạo công việc. " + Goal), Ct));
        Assert.Equal("BLOCKED", result.Status);
        Assert.Contains(await h.DbAsync(db => db.AgentToolCalls.ToArrayAsync(Ct)), x => x.ResultCode == "READ_ONLY_REQUEST");
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Deleting_conversation_removes_run_goal_and_tool_metadata()
    {
        var model = new Model { Decision = (_, n) => Task.FromResult(Call("ask_user", new { reply = "", question = "Thời lượng?", options = Array.Empty<string>() }, n)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); var conversation = Guid.NewGuid();
        await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Xếp slide"), Ct));
        var row = await h.DbAsync(db => db.AgentConversations.SingleAsync(Ct));
        await Agent(h, async s => { await s.DeleteConversationAsync(user.Id, conversation, row.Version, Ct); return true; });
        Assert.Equal(0, await h.DbAsync(db => db.AgentRuns.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.AgentToolCalls.CountAsync(Ct)));
    }

    [Fact] public async Task Optimization_stays_in_requested_day_preserves_locked_and_fixed_work_and_requires_confirmation()
    {
        var payload = JsonDocument.Parse(Draft).RootElement;
        var node = System.Text.Json.Nodes.JsonNode.Parse(payload.GetRawText())!;
        node["tasks"] = new System.Text.Json.Nodes.JsonArray(); node["reschedule"] = true;
        node["optimizationQuery"] = System.Text.Json.Nodes.JsonNode.Parse("""{"start":"2026-11-01T00:00:00Z","end":"2026-11-02T00:00:00Z"}""");
        var model = new Model { DraftText = node.ToJsonString() };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var at = DateTimeOffset.Parse("2026-11-01T08:00:00Z");
        await h.DbAsync(async db => {
            var tasks = new[] { new WorkTask { UserId = user.Id, Name = "Báo cáo", TaskType = "WRITING", Deadline = at.AddHours(10), EstimatedMinutes = 90 },
                new WorkTask { UserId = user.Id, Name = "Phiên đã khóa", TaskType = "WRITING", Deadline = at.AddHours(10), EstimatedMinutes = 60 },
                new WorkTask { UserId = user.Id, Name = "Việc ngày khác", TaskType = "WRITING", Deadline = at.AddDays(3), EstimatedMinutes = 60 } };
            db.WorkTasks.AddRange(tasks);
            db.WorkSessions.AddRange(new WorkSession { UserId = user.Id, TaskId = tasks[0].Id, Start = at.AddHours(8), End = at.AddHours(9.5) },
                new WorkSession { UserId = user.Id, TaskId = tasks[1].Id, Start = at, End = at.AddHours(1), IsLocked = true },
                new WorkSession { UserId = user.Id, TaskId = tasks[2].Id, Start = at.AddDays(1), End = at.AddDays(1).AddHours(1) });
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Lịch học", EventType = "CLASS", Start = at.AddHours(1), End = at.AddHours(2) });
            await db.SaveChangesAsync(Ct);
        });
        var before = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Tối ưu lịch ngày mai, giữ nguyên lịch học."), Ct));
        Assert.Equal("PREVIEW", result.Status); Assert.True(result.Plan!.RequiresConfirmation); Assert.True(result.Plan.IsOptimization);
        Assert.All(result.Plan.Sessions, x => Assert.True(x.Preserved || x.Start >= at && x.End <= at.Date.AddDays(1)));
        var locked = Assert.Single(before, x => x.IsLocked); Assert.Contains(result.Plan.Sessions, x => x.Id == locked.Id && x.Preserved);
        var after = await h.DbAsync(db => db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        Assert.Equal(before.OrderBy(x => x.Id).Select(x => (x.Id, x.Start, x.End, x.Status)), after.OrderBy(x => x.Id).Select(x => (x.Id, x.Start, x.End, x.Status)));
    }

    [Fact] public async Task Calendar_changes_during_model_call_invalidate_run_without_applying_a_stale_result()
    {
        var model = new Model(); await using var h = await Harness(model); var user = await h.RegisterAsync();
        model.Decision = async (_, n) => {
            if (n == 1) return Call("get_preferences", new { }, n);
            await Setup(h, user.Id);
            return Call("finish_answer", new { reply = "Câu trả lời cũ" }, n);
        };
        var failure = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Xem giờ rảnh"), Ct)));
        Assert.Equal("AGENT_STALE", failure.Code);
        Assert.Equal("FAILED", (await h.DbAsync(db => db.AgentRuns.SingleAsync(Ct))).Status);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Cancellation_releases_the_run_lease_and_records_the_attempt()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var model = new Model { Decision = (_, _) => { cancel.Cancel(); return Task.FromCanceled<NoraModelReply>(cancel.Token); } };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Xem lịch ngày mai"), cancel.Token)));
        var run = await h.DbAsync(db => db.AgentRuns.SingleAsync(Ct)); Assert.Equal("CANCELLED", run.Status);
        Assert.Equal(1, run.ModelTurns); Assert.True(run.LeaseUntil <= h.Clock.GetUtcNow());
        var failedTurn = await h.DbAsync(db => db.AgentTurns.SingleAsync(Ct)); Assert.Equal("FAILED", failedTurn.Status);
        Assert.Equal("CANCELLED", JsonSerializer.Deserialize<AgentTurnView>(failedTurn.ResultJson, Json)!.Run!.Status);
    }

    [Fact] public async Task A_second_request_cannot_continue_a_conversation_while_the_run_has_a_live_lease()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new Model { Decision = async (_, n) => { entered.SetResult(); await release.Task.WaitAsync(Ct);
            return Call("ask_user", new { reply = "", question = "Bạn muốn làm trong bao lâu?", options = Array.Empty<string>() }, n); } };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); var conversation = Guid.NewGuid();
        var first = Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Xếp lịch làm slide"), Ct));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        try
        {
            var failure = await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Xếp lịch làm bài"), Ct)));
            Assert.Equal("AGENT_BUSY", failure.Code); Assert.Equal(1, model.Calls);
        }
        finally { release.TrySetResult(); }
        Assert.Equal("NEEDS_INPUT", (await first).Status); Assert.Equal(1, await h.DbAsync(db => db.AgentRuns.CountAsync(Ct)));
    }

    [Fact] public async Task Pruned_diagnostics_do_not_prevent_undoing_an_otherwise_valid_business_change()
    {
        var model = new Model(); await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal), Ct));
        Assert.Equal("APPLIED", result.Status);
        await h.DbAsync(async db => { await db.AgentRuns.ExecuteDeleteAsync(Ct); });
        var undone = await Agent(h, s => s.UndoAgentTurnAsync(user.Id, result.Id, Ct));
        Assert.Equal("UNDONE", undone.Status); Assert.Equal("UNDONE", undone.Run!.Status);
    }

    [Theory] [InlineData("liên tục")] [InlineData("không chia nhỏ")]
    public async Task A_model_cannot_split_explicitly_continuous_work_to_make_an_infeasible_plan_fit(string phrase)
    {
        var model = new Model { DraftText = Draft.Replace("\"splittable\":false", "\"splittable\":true", StringComparison.Ordinal) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await using (var scope = h.Provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IWorkspaceService>().SetAvailabilityAsync(user.Id,
                new(Enumerable.Range(0, 7).SelectMany(day => new[] { new AvailabilityInput(day, 480, 525), new AvailabilityInput(day, 540, 585) }).ToArray()), Ct);
        var request = Goal.Replace("liên tục", phrase, StringComparison.Ordinal);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), request), Ct));
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Null(result.Plan);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Explicit_urgent_work_uses_priority_four_even_when_model_returns_default_priority()
    {
        var model = new Model(); await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var text = Goal.Replace("Làm slide", "Làm slide khẩn cấp", StringComparison.Ordinal);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), text), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(4, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).Priority);
    }

    [Fact] public async Task Rescheduling_a_saved_task_cannot_extend_its_deadline_without_an_explicit_deadline_change()
    {
        var model = new Model(); await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var deadline = DateTimeOffset.Parse("2026-11-01T10:00:00Z");
        var task = new WorkTask { UserId = user.Id, Name = "Làm slide", TaskType = "WRITING", Deadline = deadline, EstimatedMinutes = 90, Priority = 4, IsHardDeadline = true, Splittable = false };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); await db.SaveChangesAsync(Ct); });
        model.DraftText = Draft.Replace("\"taskId\":null", "\"taskId\":\"" + task.Id + "\"", StringComparison.Ordinal);
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal), Ct));
        Assert.Equal("APPLIED", result.Status);
        var saved = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct)); Assert.Equal(deadline, saved.Deadline); Assert.Equal(4, saved.Priority);
    }
}
