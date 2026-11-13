using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.UnitTests;

public sealed partial class NoraAgentRuntimeTests
{
    private static NoraModelReply Understood(NoraModelReply call, string intent, string summary = "Giải thích và hỗ trợ bạn.", bool personal = false, params string[] missing) =>
        call with { Understanding = new(intent, summary, personal, missing) };

    [Theory]
    [InlineData("Tôi đang căng thẳng vì nhiều bài quá, bạn có lời khuyên gì không?", "Advice")]
    [InlineData("Giải thích ma trận Eisenhower dùng để làm gì", "Advice")]
    public async Task Conversation_and_general_advice_need_one_model_call_zero_tools_and_no_calendar_changes(string text, string intent)
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (request, n) => {
            Assert.True(request.RequireUnderstanding);
            return Task.FromResult(Understood(Call("reply_to_user", new { reply = "Mình ở đây để hỗ trợ bạn. Hãy bắt đầu bằng một việc nhỏ trong khả năng hôm nay nhé." }, n), intent));
        } };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var phases = new List<AgentStreamEvent>();
        var turn = await Agent(h, s => s.SendAgentTurnStreamingAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), text), (item, _) => { phases.Add(item); return Task.CompletedTask; }, Ct));
        Assert.Equal("ANSWERED", turn.Status); Assert.Equal(1, model.Calls);
        Assert.Equal(0, turn.Run!.ToolCalls); Assert.Empty(turn.Run.Steps);
        Assert.Equal(intent, turn.Run.Understanding!.Intent);
        Assert.Contains(phases, p => p.Stage == "INTENT");
        Assert.DoesNotContain(phases, p => p.Type == "tool" || p.Stage == "PLANNING");
        Assert.Empty(turn.Changes); Assert.Null(turn.Plan);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.PlanProposals.ToArrayAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.AgentToolCalls.CountAsync(Ct)));
    }

    [Fact]
    public async Task Greeting_is_answered_without_a_model_call_or_workspace_changes()
    {
        var model = new Model { Decision = (_, _) => throw new InvalidOperationException("Greeting must use the local response.") };
        await using var h = await Harness(model);
        var user = await h.RegisterAsync();
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), "Xin chào Nora"), Ct));
        Assert.Equal("ANSWERED", turn.Status);
        Assert.Contains("Chào bạn", turn.Reply);
        Assert.Equal(0, model.Calls);
        Assert.Null(turn.Run);
        Assert.Empty(turn.Changes);
        Assert.Null(turn.Plan);
        Assert.False(await h.DbAsync(db => db.WorkTasks.AnyAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.PlanProposals.AnyAsync(Ct)));
    }

    [Fact] public async Task Missing_essential_details_are_asked_before_any_personal_data_tool()
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (_, n) => Task.FromResult(
            Understood(Call("get_calendar", new { start = "2026-11-01T00:00:00Z", end = "2026-11-02T00:00:00Z" }, n),
                "Clarification", "Bạn muốn đặt một cuộc họp nhưng chưa nêu ngày.", true, "Bạn muốn họp vào ngày nào?")) };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Đặt lịch họp giúp tôi"), Ct));
        Assert.Equal("NEEDS_INPUT", turn.Status); Assert.Equal("Bạn muốn họp vào ngày nào?", turn.Question);
        Assert.Equal(0, turn.Run!.ToolCalls); Assert.Equal(1, model.Calls);
        Assert.Empty(turn.Run.Steps); Assert.Empty(turn.Changes);
    }

    [Theory] [InlineData("CalendarQuery", "get_calendar")] [InlineData("FreeTimeQuery", "find_free_slots")]
    public async Task Simple_live_data_question_finishes_after_one_read_without_another_model_round(string intent, string tool)
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (_, n) => Task.FromResult(
            Understood(Call(tool, new { start = "2026-11-01T00:00:00Z", end = "2026-11-02T00:00:00Z" }, n), intent, "Xem lịch ngày mai.", true)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(),
            intent == "CalendarQuery" ? "Cho tôi xem lịch ngày mai" : "Ngày mai tôi rảnh lúc nào?"), Ct));
        Assert.Equal("ANSWERED", turn.Status); Assert.Equal(1, model.Calls); Assert.Equal(1, turn.Run!.ToolCalls);
        Assert.Contains("01/11", turn.Reply); Assert.Null(turn.Plan); Assert.Empty(turn.Changes);
    }

    [Fact] public async Task Task_lookup_uses_saved_data_and_finishes_without_a_second_model_call()
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (_, n) => Task.FromResult(
            Understood(Call("get_tasks", new { query = "Report" }, n), "TaskQuery", "Xem các công việc report.", true)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        await h.DbAsync(async db => { db.WorkTasks.Add(new() { UserId = user.Id, Name = "Report SWP", EstimatedMinutes = 90, Deadline = h.Clock.GetUtcNow().AddDays(1) }); await db.SaveChangesAsync(Ct); });
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Tôi có task report nào?"), Ct));
        Assert.Equal("ANSWERED", turn.Status); Assert.Equal(1, model.Calls); Assert.Equal(1, turn.Run!.ToolCalls);
        Assert.Contains("Report SWP", turn.Reply); Assert.Contains("90 phút", turn.Reply);
    }

    [Theory] [InlineData("")] [InlineData(" Chỉ xem trước.")]
    public async Task Clear_mutation_can_prepare_validate_and_propose_in_a_single_model_call(string suffix)
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (_, n) => Task.FromResult(
            Understood(Call("prepare_changes", new { intent = JsonDocument.Parse(Draft).RootElement }, n), "Mutation", "Xếp lịch làm slide 90 phút, hạn trưa mai.", true)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), Goal + suffix, false), Ct));
        Assert.Equal("PREVIEW", turn.Status); Assert.Equal(1, model.Calls); Assert.Equal(1, turn.Run!.ToolCalls);
        Assert.Equal(90, (Assert.Single(turn.Plan!.Sessions).End - turn.Plan.Sessions[0].Start).TotalMinutes);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task An_advice_intent_cannot_call_mutation_tools_even_if_the_model_selects_them()
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (_, n) => Task.FromResult(
            Understood(Call("propose_changes", new { intent = JsonDocument.Parse(Draft).RootElement, strategy = "keep_existing" }, n), "Advice")) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Làm sao để lên lịch học hợp lý?"), Ct));
        Assert.Equal("BLOCKED", turn.Status); Assert.Equal("AGENT_INTENT_MISMATCH", turn.ErrorCode);
        Assert.Equal(0, turn.Run!.ToolCalls); Assert.Empty(turn.Run.Steps);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
    }

    [Fact] public async Task Personal_calendar_query_cannot_be_answered_with_an_ungrounded_chat_reply()
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (_, n) => Task.FromResult(
            Understood(Call("reply_to_user", new { reply = "Bạn rảnh cả ngày." }, n), "CalendarQuery", personal: true)) };
        await using var h = await Harness(model); var user = await h.RegisterAsync();
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Lịch của tôi ngày mai thế nào?"), Ct));
        Assert.Equal("BLOCKED", turn.Status); Assert.DoesNotContain("rảnh cả ngày", turn.Reply);
        Assert.Equal(0, turn.Run!.ToolCalls);
    }

    [Fact] public async Task A_new_advice_topic_does_not_execute_the_unresolved_task_from_the_previous_turn()
    {
        var model = new Model { SupportsRequestUnderstanding = true, Decision = (request, n) => Task.FromResult(n == 1
            ? Understood(Call("ask_user", new { reply = "Mình cần thời lượng để xếp bài report.", question = "Bạn cần bao nhiêu phút?", options = Array.Empty<string>() }, n),
                "Clarification", "Bạn muốn xếp report nhưng thiếu thời lượng.", true, "Thời lượng công việc?")
            : Understood(Call("reply_to_user", new { reply = "Bạn có thể bắt đầu bằng một phần nhỏ và nghỉ ngắn khi thấy quá tải." }, n), "Advice")) };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); var conversation = Guid.NewGuid();
        var first = await Agent(h, s => s.SendAgentTurnAsync(user.Id, new(conversation, Guid.NewGuid(), "Xếp bài report cho tôi"), Ct));
        Assert.Equal("NEEDS_INPUT", first.Status);
        var request = new AgentTurnInput(conversation, Guid.NewGuid(), "Chuyện đó để sau, bây giờ tôi chỉ muốn lời khuyên khi bị quá tải.");
        var next = await Agent(h, s => s.SendAgentTurnAsync(user.Id, request, Ct));
        Assert.Equal("ANSWERED", next.Status); Assert.Equal("Advice", next.Run!.Understanding!.Intent);
        var retry = await Agent(h, s => s.SendAgentTurnAsync(user.Id, request, Ct));
        Assert.Equal(next.Id, retry.Id); Assert.Equal(2, model.Calls);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(Ct)));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
    }

    [Fact] public async Task An_explicitly_referenced_owned_task_is_available_without_a_redundant_lookup()
    {
        var model = new Model { SupportsRequestUnderstanding = true };
        await using var h = await Harness(model); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var task = new WorkTask { UserId = user.Id, Name = "Report SWP", EstimatedMinutes = 60, Deadline = h.Clock.GetUtcNow().AddDays(1), IsHardDeadline = true };
        var unrelated = new WorkTask { UserId = user.Id, Name = "Unrelated private work", EstimatedMinutes = 60, Deadline = h.Clock.GetUtcNow().AddDays(1) };
        await h.DbAsync(async db => { db.WorkTasks.AddRange(task, unrelated); await db.SaveChangesAsync(Ct); });
        model.Decision = (request, n) => {
            var data = JsonDocument.Parse(request.Contents[0].GetProperty("parts")[0].GetProperty("text").GetString()!).RootElement;
            var rows = data.GetProperty("matchingSavedTasks");
            Assert.Equal(1, rows.GetArrayLength()); Assert.Equal(task.Id, rows[0].GetProperty("id").GetGuid());
            Assert.Equal(60, rows[0].GetProperty("estimatedMinutes").GetInt32());
            var draft = System.Text.Json.Nodes.JsonNode.Parse(Draft)!;
            draft["tasks"]![0]!["taskId"] = task.Id.ToString(); draft["tasks"]![0]!["name"] = task.Name;
            draft["tasks"]![0]!["deadline"] = null; draft["tasks"]![0]!["deadlineEvidence"] = "";
            return Task.FromResult(Understood(Call("propose_changes", new { intent = JsonSerializer.SerializeToElement(draft), strategy = "keep_existing" }, n), "Mutation", "Cập nhật report lên 90 phút.", true));
        };
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), $"Cập nhật task Report SWP (mã {task.Id}) thành 90 phút, làm liên tục, chỉ xem trước."), Ct));
        Assert.Equal("PREVIEW", turn.Status); Assert.Equal(1, model.Calls);
        Assert.Single(turn.Run!.Steps); Assert.Equal("propose_changes", turn.Run.Steps[0].Tool);
        Assert.Equal(60, await h.DbAsync(db => db.WorkTasks.Where(t => t.Id == task.Id).Select(t => t.EstimatedMinutes).SingleAsync(Ct)));
    }
}
