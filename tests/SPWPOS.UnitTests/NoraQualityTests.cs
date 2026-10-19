using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class NoraQualityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Agent<T>(ServiceHarness h, Func<IPlanningAgentService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IPlanningAgentService>()); }
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    private static Task Work(ServiceHarness h, Func<IWorkspaceService, Task> action) => Work(h, async s => { await action(s); return true; });
    private static async Task<ServiceHarness> Harness(AgentAiTestDouble ai)
    {
        var h = await ServiceHarness.CreateAsync(s => s.AddSingleton<ITaskInterpreter>(ai));
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-19T01:00:00Z") - h.Clock.GetUtcNow());
        return h;
    }
    private static Task<AgentTurnView> Send(ServiceHarness h, Guid uid, Guid conversation, string text, bool apply = true) =>
        Agent(h, s => s.SendAgentTurnAsync(uid, new(conversation, Guid.NewGuid(), text, apply), Ct));
    private static async Task Setup(ServiceHarness h, Guid uid)
    {
        await Work(h, s => s.SetAvailabilityAsync(uid, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 1140, 1320)).ToArray()), Ct));
        await Work(h, s => s.SetPreferenceAsync(uid, new("Asia/Ho_Chi_Minh", 25, 240, 0, 1140, 1320, MaximumDailyMinutes: 480), Ct));
    }
    private static DateTimeOffset At(int day, int hour) => DateTimeOffset.Parse($"2026-10-{day:00}T{hour:00}:00:00+07:00");

    [Theory] [InlineData("WORK", "WORK")] [InlineData("NORA_TEST_UNKNOWN", "OTHER")]
    public async Task An_explicit_task_category_is_resolved_even_when_the_model_selects_a_different_active_category(string requested, string expected)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Hồ sơ khách hàng", At(20, 22), 45, 2, "WRITING", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, Guid.NewGuid(), $"Tạo hồ sơ khách hàng 45 phút, hạn chót 22h ngày mai, loại {requested}.");
        Assert.Equal("APPLIED", result.Status); Assert.Equal(expected, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).TaskType);
        Assert.DoesNotContain(result.Warnings, x => x.Contains("WRITING", StringComparison.Ordinal));
    }

    [Fact] public async Task Applying_a_preview_keeps_the_explanation_and_persists_a_separate_verified_outcome()
    {
        const string explanation = "Mình đã hiểu bạn muốn xếp hồ sơ khách hàng. Mình ưu tiên hồ sơ khách hàng trước để bạn còn thời gian kiểm tra kết quả.";
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Reply = explanation,
            Tasks = [new(null, "Xử lý hồ sơ khách hàng", At(20, 22), 45, 2, "WORK", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var conversation = Guid.NewGuid();
        var preview = await Send(h, user.Id, conversation, "Hồ sơ khách hàng 45 phút, hạn chót 22h ngày mai", false);
        Assert.Equal("PREVIEW", preview.Status); Assert.Equal(explanation, preview.Reply);
        var saved = await Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal("APPLIED", saved.Status); Assert.Equal(explanation, saved.Reply);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(saved, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var outcome = json.RootElement.GetProperty("outcome").GetString()!;
        Assert.Contains("Công việc", outcome); Assert.Contains("45", outcome); Assert.Contains("20/10/2026", outcome);
        var history = await Agent(h, s => s.GetAgentTurnsAsync(user.Id, Ct));
        Assert.Equal(explanation, Assert.Single(history).Reply);
        Assert.Equal(json.RootElement.GetProperty("outcome").GetString(), JsonSerializer.SerializeToElement(history[0], new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("outcome").GetString());
    }

    [Theory] [InlineData("task", "WORK")] [InlineData("task", "NORA_TEST_UNKNOWN")] [InlineData("event", "WORK")]
    public async Task Saved_actions_have_a_readable_confirmation_and_explain_category_substitution(string kind, string category)
    {
        const string name = "Xử lý hồ sơ";
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Reply = kind == "event" ? "Mình đã lên lịch cho bạn." : "Đã lưu các thay đổi.",
            Tasks = kind == "task" ? [new(null, name, At(20, 22), 45, 2, category, true)] : [],
            Events = kind == "event" ? [new(name, At(20, 13), At(20, 17), EventType: category)] : [] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await Send(h, user.Id, Guid.NewGuid(), kind == "task"
            ? $"Tạo và xếp {name} 45 phút, hạn chót 22h ngày mai, loại {category}."
            : $"Thêm {name} ngày mai từ 13h đến 17h chiều.", false);
        Assert.Equal("PREVIEW", preview.Status); Assert.NotEmpty(preview.Reply);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        var applied = await Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct));
        Assert.Equal("APPLIED", applied.Status); Assert.Contains(name, applied.Reply); Assert.NotEqual(preview.Reply, applied.Reply);
        Assert.Contains(category == "WORK" ? "Công việc" : "Khác", applied.Outcome); Assert.DoesNotContain("[WORK]", applied.Outcome);
        if (kind == "task")
        {
            var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
            Assert.Equal(2, task.Priority); Assert.True(task.Splittable); Assert.Equal(45, task.EstimatedMinutes);
            Assert.DoesNotContain("ưu tiên 2", Assert.Single(applied.Changes).After);
            Assert.DoesNotContain("có thể chia phiên", applied.Outcome);
            if (category == "NORA_TEST_UNKNOWN")
            {
                Assert.Equal("OTHER", task.TaskType);
                Assert.Contains(applied.Warnings, x => x.Contains(category, StringComparison.Ordinal) && x.Contains("Khác", StringComparison.Ordinal));
            }
        }
        var history = Assert.Single(await Agent(h, s => s.GetAgentTurnsAsync(user.Id, Ct)));
        Assert.Equal(applied.Reply, history.Reply); Assert.Equal(applied.Warnings, history.Warnings);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Editing_an_event_preserves_valid_sessions_unless_a_global_replan_was_requested(bool global)
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await Work(h, s => s.SetAvailabilityAsync(user.Id, new([new(2, 420, 1080)]), Ct));
        await Work(h, s => s.SetPreferenceAsync(user.Id, new("Asia/Ho_Chi_Minh", 25, 90, 10, 480, 1080, MaximumDailyMinutes: 480), Ct));
        var id = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Đi làm", At(20, 13), At(20, 17), EventType: "WORK"), Ct));
        var task = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Kiểm tra danh sách", "", At(20, 22), 30, IsHardDeadline: true), Ct));
        var session = new WorkSession { UserId = user.Id, TaskId = task, Start = At(20, 17), End = At(20, 17).AddMinutes(30) };
        await h.DbAsync(async db => { db.WorkSessions.Add(session); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Reschedule = true, ScheduleTaskIds = [task],
            Events = [new("Đi làm", At(20, 13).AddMinutes(30), At(20, 17), EventType: "WORK", ExistingId: id)] };
        var text = "Đổi giờ đi làm ngày mai thành 13h30–17h, giữ các thông tin khác." + (global ? " Đồng thời tối ưu lại toàn bộ lịch công việc." : "");
        var result = await Send(h, user.Id, Guid.NewGuid(), text);
        Assert.Equal("APPLIED", result.Status);
        var scheduled = await h.DbAsync(db => db.WorkSessions.AsNoTracking().Where(x => x.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
        if (global) Assert.NotEqual(session.Start, Assert.Single(scheduled).Start);
        else { Assert.Equal(session.Id, Assert.Single(scheduled).Id); Assert.Equal(session.Start, scheduled[0].Start); Assert.Equal(session.End, scheduled[0].End); }
        Assert.Equal(At(20, 13).AddMinutes(30), (await h.DbAsync(db => db.FixedEvents.AsNoTracking().SingleAsync(Ct))).Start);
    }

    [Theory] [InlineData("clock")] [InlineData("clock-numeric")] [InlineData("category")]
    public async Task A_question_about_a_detail_already_resolved_by_server_facts_is_rechecked_before_asking_the_user(string kind)
    {
        var ai = new AgentAiTestDouble();
        var clock = kind.StartsWith("clock", StringComparison.Ordinal);
        ai.Response = _ => ai.Context.Contains("serverResolvedFacts", StringComparison.Ordinal)
            ? clock ? AgentAiTestDouble.Empty() with { Events = [new("Đi học", At(20, 7), At(20, 12), EventType: "CLASS")] }
                : AgentAiTestDouble.Empty() with { Tasks = [new(null, "Kiểm tra danh sách", At(20, 22), 30, 2, "OTHER", true)] }
            : AgentAiTestDouble.Empty() with { Question = kind switch {
                "clock" => "Bạn muốn 7h–12h sáng hay tối?",
                "clock-numeric" => "Bạn có ý định học từ 07:00 đến 12:00 hay từ 19:00 đến 24:00 vào ngày mai?",
                _ => "Bạn chọn danh mục nào?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var prompt = clock ? "Ngày mai lên lịch cho tôi đi học từ 7h đến 12h sáng."
            : "Tạo và xếp việc kiểm tra danh sách, cần 30 phút, hạn chót 22h ngày mai, mã loại NORA_TEST_UNKNOWN.";
        var result = await Send(h, user.Id, Guid.NewGuid(), prompt);
        Assert.Equal("APPLIED", result.Status); Assert.Empty(result.Question);
        if (clock)
        {
            var saved = await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct));
            Assert.Equal(At(20, 7), saved.Start); Assert.Equal(At(20, 12), saved.End); Assert.Equal("CLASS", saved.EventType);
        }
        else Assert.Equal("OTHER", (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).TaskType);
    }

    [Theory] [InlineData("WORK")] [InlineData("UNSUPPORTED")] [InlineData("")] [InlineData(null)]
    public async Task Morning_class_then_afternoon_work_shift_save_exact_fixed_events_without_flexible_tasks(string? category)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Đi học", At(20, 7), At(20, 12), EventType: "CLASS")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var conversation = Guid.NewGuid();
        var morning = await Send(h, user.Id, conversation, "ngày mai lên lịch cho tôi đi học từ 7h đến 12h sáng");
        Assert.Equal("APPLIED", morning.Status);
        var original = await h.DbAsync(db => db.FixedEvents.AsNoTracking().SingleAsync(Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Đi làm", At(20, 13), At(20, 17), EventType: category!)]
        };

        var afternoon = await Send(h, user.Id, conversation, "lên lịch chiều mai tôi đi làm từ 13h -17h");

        Assert.Equal("APPLIED", afternoon.Status);
        var events = await h.DbAsync(db => db.FixedEvents.AsNoTracking().OrderBy(x => x.Start).ToArrayAsync(Ct));
        Assert.Equal(2, events.Length);
        Assert.Equal(original.Id, events[0].Id); Assert.Equal(At(20, 7), events[0].Start); Assert.Equal(At(20, 12), events[0].End);
        Assert.Equal("CLASS", events[0].EventType);
        Assert.Equal(At(20, 13), events[1].Start); Assert.Equal(At(20, 17), events[1].End); Assert.Equal(category == "WORK" ? "WORK" : "OTHER", events[1].EventType);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Theory] [InlineData("WORK", "WORK")] [InlineData("WRITING", "WRITING")] [InlineData("DESIGN", "OTHER")]
    [InlineData("UNKNOWN", "OTHER")] [InlineData("", "OTHER")] [InlineData(null, "OTHER")]
    public async Task Agent_task_categories_use_the_current_catalog_and_persist_the_normalized_command(string? category, string expected)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Tasks = [new(null, "Slide", At(21, 22), 45, 2, category!, true)]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, Guid.NewGuid(), "Slide 45 phút, hạn chót 22h ngày 21/10");
        Assert.Equal("APPLIED", result.Status);
        var task = await h.DbAsync(db => db.WorkTasks.AsNoTracking().SingleAsync(Ct));
        Assert.Equal(expected, task.TaskType); Assert.Equal(45, task.EstimatedMinutes); Assert.Equal(At(21, 22), task.Deadline);
        Assert.NotEmpty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        var stored = await h.DbAsync(db => db.AgentTurns.AsNoTracking().SingleAsync(x => x.Id == result.Id, Ct));
        var intent = JsonSerializer.Deserialize<AgentIntent>(stored.CommandJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(expected, Assert.Single(intent.Tasks).TaskType);
    }

    [Theory] [InlineData("task", "", "WRITING")] [InlineData("event", "", "CLASS")]
    [InlineData("task", "UNSUPPORTED", "OTHER")] [InlineData("event", "UNSUPPORTED", "OTHER")]
    [InlineData("task", "WORK", "WORK")] [InlineData("event", "WORK", "WORK")]
    public async Task Agent_edits_preserve_omitted_categories_and_resolve_explicit_categories(string kind, string category, string expected)
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        if (kind == "task")
        {
            var id = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Slide", "", At(21, 22), 45, TaskType: "WRITING"), Ct));
            ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(id, "Slide", At(22, 22), null, null, category, null)] };
        }
        else
        {
            var id = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Đi học", At(20, 7), At(20, 12), EventType: "CLASS"), Ct));
            ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Đi học", At(20, 8), At(20, 12), EventType: category, ExistingId: id)] };
        }
        var result = await Send(h, user.Id, Guid.NewGuid(), kind == "task" ? "Hạn chót Slide là 22h ngày 22/10" : "Đi học ngày 20/10 bắt đầu 8h, kết thúc 12h");
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(expected, kind == "task"
            ? await h.DbAsync(db => db.WorkTasks.Select(x => x.TaskType).SingleAsync(Ct))
            : await h.DbAsync(db => db.FixedEvents.Select(x => x.EventType).SingleAsync(Ct)));
    }

    [Fact] public async Task Unknown_agent_task_category_cannot_activate_a_disabled_other_category()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Slide", At(21, 22), 45, 2, "UNSUPPORTED", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => { (await db.TaskTypes.SingleAsync(x => x.Code == "OTHER", Ct)).IsActive = false; await db.SaveChangesAsync(Ct); });
        var error = await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, Guid.NewGuid(), "Slide 45 phút, hạn chót 22h ngày 21/10"));
        Assert.Equal("INVALID_TASK_TYPE", error.Code);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct))); Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }

    [Theory] [InlineData("DESIGN")] [InlineData("OTHER")]
    public async Task An_explicit_unavailable_category_with_no_substitution_requires_clarification(string modelCategory)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Thiết kế poster", At(21, 22), 60, 2, modelCategory, true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, Guid.NewGuid(), "Tạo việc Thiết kế poster 60 phút, hạn chót 22h ngày 21/10. Bắt buộc mã loại là DESIGN; nếu hệ thống chưa có thì cứ tự thêm mã đó, không dùng loại khác.");
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.NotEmpty(result.Question);
        Assert.Empty(result.Changes); Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.TaskTypes.AnyAsync(x => x.Code == "DESIGN" && x.IsActive, Ct)));
    }

    [Fact] public async Task Category_fallback_does_not_allow_an_overlapping_event_or_partial_changes()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with {
            Events = [new("Đi làm", At(20, 11), At(20, 17), EventType: "UNSUPPORTED")]
        } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await Work(h, s => s.SaveEventAsync(user.Id, null, new("Đi học", At(20, 7), At(20, 12), EventType: "CLASS"), Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, Guid.NewGuid(), "Đi làm ngày mai 11h–17h"));
        Assert.Equal("FIXED_EVENT_OVERLAP", error.Code);
        Assert.Contains("Đi học", error.Message); Assert.Contains("20/10/2026 07:00", error.Message);
        Assert.Equal("Đi học", Assert.Single(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct))).Name);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Clarification_recovery_is_bounded_and_does_not_apply_an_unresolved_request()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "Bạn chọn danh mục nào?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, Guid.NewGuid(), "Tạo việc kiểm tra danh sách 30 phút, hạn chót 22h ngày mai, mã loại UNKNOWN.");
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Empty(result.Changes);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        // Both attempts are accounted for; no unbounded free retries are possible.
        Assert.Equal(2, await h.DbAsync(db => db.AiUsageRecords.CountAsync(Ct)));
    }

    [Theory]
    [InlineData("Ngày mai đi học từ 7h đến 12h.", "Bạn muốn sáng hay tối?")]
    [InlineData("Ngày mai đi học từ 12h đến 7h sáng.", "Bạn muốn sáng hay tối?")]
    [InlineData("Ngày mai đi học từ 7h đến 12h sáng, gặp bạn lúc 8h30.", "Bạn muốn sáng hay tối?")]
    [InlineData("Ngày mai đi học từ 7h đến 12h30 sáng.", "Bạn muốn sáng hay tối?")]
    [InlineData("Tạo việc mã loại UNKNOWN, bắt buộc không dùng loại khác.", "Bạn chọn danh mục nào?")]
    [InlineData("Tạo việc mã loại UNKNOWN.", "Bạn chọn danh mục nào và cần bao nhiêu phút?")]
    public async Task Genuine_missing_or_ambiguous_details_remain_questions_without_a_recovery_call(string prompt, string question)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = question } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var result = await Send(h, user.Id, Guid.NewGuid(), prompt);
        Assert.Equal("NEEDS_INPUT", result.Status); Assert.Empty(result.Changes);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.AiUsageRecords.CountAsync(Ct)));
    }

    [Fact] public async Task Mixed_clarification_recovers_to_a_question_with_no_mutations_in_either_apply_mode()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "Buổi slide đã được lưu chưa?", Reschedule = true,
            Tasks = [new(null, "Bài tập", At(22, 22), 60, 2, "STUDY", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var revision = (await Work(h, s => s.GetAsync(user.Id, Ct))).Preference.Revision;
        foreach (var automatic in new[] { false, true })
        {
            var result = await Send(h, user.Id, Guid.NewGuid(), "Xếp bài tập, đừng trùng slide", automatic);
            Assert.Equal("NEEDS_INPUT", result.Status); Assert.Empty(result.Changes); Assert.Null(result.Plan);
        }
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
        Assert.Equal(revision, (await Work(h, s => s.GetAsync(user.Id, Ct))).Preference.Revision);
    }

    [Fact] public async Task Invented_duration_is_rejected_and_followup_keeps_Thursday_and_changes_deadline_to_21()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Ôn tiếng Anh", At(22, 22), 180, 2, "STUDY", true,
            DurationEvidence: "", DeadlineEvidence: "", WorkWindowEvidence: "Tối thứ Năm 22/10")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        var first = await Send(h, user.Id, conversation, "Tối thứ Năm 22/10 mình muốn ôn tiếng Anh. Xếp giúp mình nhé.");
        Assert.Equal("NEEDS_INPUT", first.Status); Assert.Contains("bao nhiêu phút", first.Question);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Ôn tiếng Anh", null, 45, 2, "STUDY", true,
            DurationEvidence: "45 phút", DeadlineEvidence: "", WorkWindowEvidence: "Tối thứ Năm 22/10")] };
        var applied = await Send(h, user.Id, conversation, "Khoảng 45 phút thôi, mình muốn học xong trước 21h.");
        Assert.Equal("APPLIED", applied.Status);
        using var context = JsonDocument.Parse(ai.Context); Assert.Equal("Tối thứ Năm 22/10 mình muốn ôn tiếng Anh. Xếp giúp mình nhé.", context.RootElement.GetProperty("pendingRequest").GetProperty("originalText").GetString());
        var task = Assert.Single(await h.DbAsync(db => db.WorkTasks.AsNoTracking().ToArrayAsync(Ct)));
        Assert.Equal(45, task.EstimatedMinutes); Assert.Equal(At(22, 21), task.Deadline);
        Assert.Equal(At(22, 18), task.WorkWindowStart); Assert.Equal(At(23, 0), task.WorkWindowEnd);
        var session = Assert.Single(applied.Plan!.Sessions); Assert.Equal(At(22, 19), session.Start); Assert.Equal(session.Start.AddMinutes(45), session.End);
        Assert.Contains("22/10/2026 19:00", applied.Outcome);
        var replan = await Work(h, s => s.GeneratePlanAsync(user.Id, new(h.Clock.GetUtcNow().AddMinutes(5), 7, UseCapacityFactor: false, UseEstimateBias: false), Ct));
        Assert.All(replan.Sessions, x => Assert.True(x.Start >= At(22, 18) && x.End <= At(22, 21)));
        var edit = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.EditSessionAsync(user.Id, session.Id, new(At(20, 19), At(20, 19).AddMinutes(45)), Ct)));
        Assert.Equal("TASK_WORK_WINDOW", edit.Code);
    }

    [Fact] public async Task Followup_updates_existing_task_duration_and_deadline_without_losing_work_window()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var id = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Ôn tiếng Anh", "", At(22, 22), 180, TaskType: "STUDY"), Ct));
        await h.DbAsync(async db => { var task = await db.WorkTasks.SingleAsync(Ct); task.WorkWindowStart = At(22, 18); task.WorkWindowEnd = At(23, 0); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(id, "Ôn tiếng Anh", null, 45, null, "", null, DurationEvidence: "45 phút", DeadlineEvidence: "", WorkWindowEvidence: "")] };
        var result = await Send(h, user.Id, Guid.NewGuid(), "Khoảng 45 phút thôi, mình muốn học xong trước 21h.");
        Assert.Equal("APPLIED", result.Status); var task = await h.DbAsync(db => db.WorkTasks.AsNoTracking().SingleAsync(Ct));
        Assert.Equal(At(22, 21), task.Deadline); Assert.Equal(At(22, 18), task.WorkWindowStart);
        Assert.Equal(At(22, 19), Assert.Single(result.Plan!.Sessions).Start);
    }

    [Theory] [InlineData("6", 18)] [InlineData("sáu", 18)] [InlineData("8", 20)]
    public async Task Ambiguous_event_time_never_writes_then_clarification_creates_one_event(string hour, int clarified)
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Hẹn với thầy", At(21, clarified), At(21, clarified).AddMinutes(30), EventType: "APPOINTMENT")] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); var conversation = Guid.NewGuid();
        var first = await Send(h, user.Id, conversation, $"Thứ Tư 21/10 mình có hẹn với thầy lúc {hour} giờ, kéo dài nửa tiếng.");
        Assert.Equal("NEEDS_INPUT", first.Status); Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        var result = await Send(h, user.Id, conversation, $"À, {clarified}h chiều nhé.");
        Assert.Equal("APPLIED", result.Status); Assert.Single(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.False(first.CanUndo);
    }

    [Fact] public async Task No_change_event_and_task_requests_keep_revision_sessions_and_undo_unchanged()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var eventId = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Hẹn", At(21, 18), At(21, 18).AddMinutes(30), EventType: "APPOINTMENT"), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Hẹn", At(21, 18), At(21, 18).AddMinutes(30), EventType: "APPOINTMENT", ExistingId: eventId)] };
        var before = await Work(h, s => s.GetAsync(user.Id, Ct));
        var result = await Send(h, user.Id, Guid.NewGuid(), "À, 6 giờ chiều nhé.");
        Assert.Equal("ANSWERED", result.Status); Assert.Empty(result.Changes); Assert.False(result.CanUndo);
        Assert.Equal(before.Preference.Revision, (await Work(h, s => s.GetAsync(user.Id, Ct))).Preference.Revision);
        Assert.Single(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        var taskId = await Work(h, s => s.SaveTaskAsync(user.Id, null, new("Slide", "", At(21, 22), 90, TaskType: "WRITING", IsHardDeadline: true), Ct));
        ai.Response = _ => AgentAiTestDouble.Empty() with { ScheduleTaskIds = [taskId] };
        var scheduled = await Send(h, user.Id, Guid.NewGuid(), "Xếp lịch Slide"); Assert.Equal("APPLIED", scheduled.Status);
        before = await Work(h, s => s.GetAsync(user.Id, Ct));
        result = await Send(h, user.Id, Guid.NewGuid(), "Xếp lại lịch Slide");
        Assert.Equal("ANSWERED", result.Status); Assert.Null(result.Plan); Assert.False(result.CanUndo);
        var after = await Work(h, s => s.GetAsync(user.Id, Ct)); Assert.Equal(before.Preference.Revision, after.Preference.Revision);
        Assert.Equal(before.Sessions.Select(x => x.Id), after.Sessions.Select(x => x.Id));
    }

    [Fact] public async Task Failed_requests_are_visible_and_missing_work_cannot_be_declared_deadline_safe()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Làm slide", At(18, 22), 90, 2, "WRITING", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        Assert.Equal("DEADLINE_CONFLICT", (await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, conversation, "Slide 90 phút, xong trước 22h ngày 18/10"))).Code);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Reply = "Tất cả deadline đều an toàn." };
        var result = await Send(h, user.Id, conversation, "Làm slide còn kịp deadline không?");
        Assert.Equal("ANSWERED", result.Status); Assert.Contains("chưa thể xác nhận", result.Reply);
        Assert.Contains(ai.History, x => x.Status == "FAILED");
        using var context = JsonDocument.Parse(ai.Context); Assert.DoesNotContain(context.RootElement.GetProperty("activeTaskTypes").EnumerateArray(), x => x.GetString() == "DESIGN");
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Exclusive_Friday_availability_overrides_recurring_hours_and_continuous_two_hours_is_blocked()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Exceptions = [new(new(2026, 10, 23), 1200, 1260, true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        Assert.Equal("APPLIED", (await Send(h, user.Id, Guid.NewGuid(), "Riêng thứ Sáu 23/10 mình chỉ rảnh từ 20h đến 21h, ngoài khoảng đó bận hết.")).Status);
        var exception = await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().SingleAsync(Ct)); Assert.True(exception.OverridesWeeklyAvailability);
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Thi thử", At(23, 22), 120, 2, "STUDY", false, At(23, 18), At(24, 0))] };
        var result = await Send(h, user.Id, Guid.NewGuid(), "Thi thử 2 tiếng liên tục vào tối thứ Sáu 23/10, xong trước 22h.");
        Assert.Equal("BLOCKED", result.Status); Assert.False(result.CanUndo); Assert.NotEmpty(result.Warnings);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Disabling_a_task_type_after_preview_blocks_apply_atomically()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Slide", At(21, 22), 90, 2, "WRITING", true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var preview = await Send(h, user.Id, Guid.NewGuid(), "Làm slide 90 phút", false); Assert.Equal("PREVIEW", preview.Status);
        await h.DbAsync(async db => { var type = await db.TaskTypes.SingleAsync(x => x.Code == "WRITING", Ct); type.IsActive = false; await db.SaveChangesAsync(Ct); });
        Assert.Equal("INVALID_TASK_TYPE", (await Assert.ThrowsAsync<AuthException>(() => Agent(h, s => s.ApplyAgentTurnAsync(user.Id, preview.Id, Ct)))).Code);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)));
    }
    [Fact] public async Task Multiple_clarification_answers_are_retained_as_bounded_structured_context()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Question = "Bạn dành bao nhiêu phút?" } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        await Send(h, user.Id, conversation, "Làm slide cho tôi");
        ai.Response = _ => AgentAiTestDouble.Empty() with { Question = "Deadline vào ngày giờ nào?" };
        await Send(h, user.Id, conversation, "90 phút");
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Làm slide", At(21, 22), 90, 2, "WRITING", true,
            DurationEvidence: "90 phút", DeadlineEvidence: "xong trước 22h ngày 21/10", WorkWindowEvidence: "")] };
        var result = await Send(h, user.Id, conversation, "xong trước 22h ngày 21/10");
        Assert.Equal("APPLIED", result.Status);
        using var context = JsonDocument.Parse(ai.Context); var pending = context.RootElement.GetProperty("pendingRequest");
        Assert.Contains(pending.GetProperty("answers").EnumerateArray(), x => x.GetString() == "90 phút");
        Assert.Equal(90, pending.GetProperty("estimatedMinutes").GetInt32());
        Assert.Equal(90, Assert.Single(await h.DbAsync(db => db.WorkTasks.AsNoTracking().ToArrayAsync(Ct))).EstimatedMinutes);
    }

    [Fact] public async Task Replacing_exclusive_date_hours_removes_old_windows_and_undo_restores_them()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Exceptions = [new(new(2026, 10, 23), 1200, 1260, true)] } };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id); var conversation = Guid.NewGuid();
        var first = await Send(h, user.Id, conversation, "23/10 chỉ rảnh 20h–21h"); Assert.Equal("APPLIED", first.Status);
        var old = Assert.Single(await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().ToArrayAsync(Ct)));
        ai.Response = _ => AgentAiTestDouble.Empty() with { Exceptions = [new(new(2026, 10, 23), 1260, 1320, true)] };
        var second = await Send(h, user.Id, conversation, "23/10 chỉ rảnh 21h–22h"); Assert.Equal("APPLIED", second.Status);
        var updated = Assert.Single(await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().ToArrayAsync(Ct)));
        Assert.Equal(1260, updated.StartMinute); Assert.True(updated.OverridesWeeklyAvailability);
        await Agent(h, s => s.UndoAgentTurnAsync(user.Id, second.Id, Ct));
        var restored = Assert.Single(await h.DbAsync(db => db.AvailabilityExceptions.AsNoTracking().ToArrayAsync(Ct)));
        Assert.Equal(old.Id, restored.Id); Assert.Equal(old.StartMinute, restored.StartMinute); Assert.True(restored.OverridesWeeklyAvailability);
    }
}
