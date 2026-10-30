using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    private static AgentAiTestDouble OptimizationAi(AgentFreeTimeQuery? query = null) => new() {
        Response = _ => AgentAiTestDouble.Empty() with { Reschedule = true, OptimizationQuery = query ?? Query, ResultKind = "MUTATION" }
    };

    [Theory] [InlineData("Chỉ xem giờ rảnh", false)] [InlineData("Không tối ưu lịch", false)]
    [InlineData("Tối ưu lịch theo khoảng đã chọn", true)]
    public async Task Unrequested_or_out_of_range_optimization_is_clarified_without_changes(string request, bool farFuture)
    {
        var ai = OptimizationAi(farFuture ? new(Query.Start.AddDays(50), Query.End.AddDays(50)) : Query);
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var turn = await Send(h, user.Id, request);
        Assert.Equal("NEEDS_INPUT", turn.Status); Assert.Null(turn.Plan); Assert.Empty(turn.Changes);
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Nora_optimization_previews_old_and_new_hours_then_applies_and_undoes_without_moving_protected_or_outside_work(bool streaming)
    {
        var ai = OptimizationAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var task = new WorkTask { UserId = user.Id, Name = "Báo cáo", EstimatedMinutes = 60, Deadline = At(1320), Splittable = false };
        var lockedTask = new WorkTask { UserId = user.Id, Name = "Đã khóa", EstimatedMinutes = 60, Deadline = At(1320) };
        var outsideTask = new WorkTask { UserId = user.Id, Name = "Ngày khác", EstimatedMinutes = 60, Deadline = At(1320).AddDays(2) };
        var old = new WorkSession { UserId = user.Id, TaskId = task.Id, Start = At(540), End = At(600) };
        var locked = new WorkSession { UserId = user.Id, TaskId = lockedTask.Id, Start = At(600), End = At(660), IsLocked = true };
        var outside = new WorkSession { UserId = user.Id, TaskId = outsideTask.Id, Start = At(1140).AddDays(1), End = At(1200).AddDays(1) };
        await h.DbAsync(async db => {
            var preference = await db.WorkPreferences.SingleAsync(Ct); preference.PreferredPeriod = "EVENING";
            db.WeeklyAvailability.Add(new() { UserId = user.Id, DayOfWeek = (int)Day.DayOfWeek, StartMinute = 540, EndMinute = 660 });
            db.WorkTasks.AddRange(task, lockedTask, outsideTask); db.WorkSessions.AddRange(old, locked, outside);
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Lớp học", Start = At(1140), End = At(1200) });
            await db.SaveChangesAsync(Ct);
        });
        var revision = await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct));
        var turn = await Send(h, user.Id, "Tối ưu lịch ngày mai cho tôi", streaming);
        Assert.Equal("PREVIEW", turn.Status); Assert.True(turn.Plan!.IsOptimization); Assert.True(turn.Plan.RequiresConfirmation);
        Assert.Equal(At(0), turn.Plan.Start); Assert.Equal(At(1440), turn.Plan.End);
        var moved = Assert.Single(turn.Changes, change => change.TaskId == task.Id);
        Assert.Contains("09:00", moved.Before); Assert.Contains("20:00", moved.After);
        Assert.Contains("Xác nhận kế hoạch", turn.Reply);
        Assert.Contains(turn.Plan.Sessions, x => x.Id == locked.Id && x.Preserved);
        Assert.DoesNotContain(turn.Plan.Sessions, x => x.TaskId == outsideTask.Id);
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Select(x => x.Revision).SingleAsync(Ct)));
        Assert.Equal(WorkSessionStatus.Scheduled, await h.DbAsync(db => db.WorkSessions.Where(x => x.Id == old.Id).Select(x => x.Status).SingleAsync(Ct)));

        await using var scope = h.Provider.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IPlanningAgentService>();
        // Retrying with auto-save still cannot apply an optimization implicitly.
        var retried = await service.SendAgentTurnAsync(user.Id, new(turn.ConversationId, turn.RequestId, turn.Text, true), Ct);
        Assert.Equal("PREVIEW", retried.Status); Assert.Equal(1, ai.Calls);
        var saved = await service.ApplyAgentTurnAsync(user.Id, turn.Id, Ct);
        Assert.Equal("APPLIED", saved.Status); Assert.True(saved.CanUndo); Assert.DoesNotContain("Bấm Xác nhận", saved.Reply);
        await h.DbAsync(async db => {
            Assert.Equal(WorkSessionStatus.Cancelled, (await db.WorkSessions.SingleAsync(x => x.Id == old.Id, Ct)).Status);
            Assert.Equal(outside.Start, (await db.WorkSessions.SingleAsync(x => x.Id == outside.Id, Ct)).Start);
            Assert.True((await db.WorkSessions.SingleAsync(x => x.Id == locked.Id, Ct)).IsLocked);
            Assert.Equal(At(1140), (await db.FixedEvents.SingleAsync(Ct)).Start);
            Assert.Equal(At(1320), (await db.WorkTasks.SingleAsync(x => x.Id == task.Id, Ct)).Deadline);
        });
        var undone = await service.UndoAgentTurnAsync(user.Id, turn.Id, Ct); Assert.Equal("UNDONE", undone.Status);
        Assert.Equal(WorkSessionStatus.Scheduled, await h.DbAsync(db => db.WorkSessions.Where(x => x.Id == old.Id).Select(x => x.Status).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Today_optimization_stops_at_local_midnight_and_does_not_spill_into_tomorrow()
    {
        var query = new AgentFreeTimeQuery(At(0).AddDays(-1), At(0));
        var ai = OptimizationAi(query); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        h.Clock.Advance(At(0).AddDays(-1).AddHours(18) - h.Clock.GetUtcNow());
        await h.DbAsync(async db => {
            db.WeeklyAvailability.Add(new() { UserId = user.Id, DayOfWeek = (int)Day.AddDays(-1).DayOfWeek, StartMinute = 1140, EndMinute = 1320 });
            db.WorkTasks.Add(new() { UserId = user.Id, Name = "Việc dài", EstimatedMinutes = 240, Deadline = At(1320), Splittable = true });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id, "Tối ưu lịch hôm nay");
        Assert.Equal("BLOCKED", turn.Status); Assert.Equal(At(0), turn.Plan!.End);
        Assert.All(turn.Plan.Sessions, session => Assert.True(session.End <= At(0)));
        Assert.NotEmpty(turn.Warnings); Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }

    [Fact]
    public async Task An_unchanged_optimization_reports_no_change_instead_of_creating_an_empty_proposal()
    {
        var ai = OptimizationAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            var task = new WorkTask { UserId = user.Id, Name = "Đã hợp lý", EstimatedMinutes = 60, Deadline = At(1320) }; db.WorkTasks.Add(task);
            db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(1140), End = At(1200) });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id, "Tối ưu lịch ngày mai");
        Assert.Equal("ANSWERED", turn.Status); Assert.Null(turn.Plan); Assert.Empty(turn.Changes);
        Assert.Contains("chưa tìm được cách xếp khác", turn.Reply); Assert.False(turn.CanUndo);
        Assert.Equal(0, await h.DbAsync(db => db.PlanProposals.CountAsync(Ct)));
    }

    [Theory] [InlineData("tuần này", 0)] [InlineData("tuần sau", 7)]
    public async Task Week_optimization_uses_account_local_Monday_boundaries(string label, int offset)
    {
        var ai = OptimizationAi(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        await h.DbAsync(async db => {
            db.WorkTasks.Add(new() { UserId = user.Id, Name = "Việc tuần", EstimatedMinutes = 60, Deadline = At(1320).AddDays(14) });
            for (var day = 0; day < 7; day++) db.WeeklyAvailability.Add(new() { UserId = user.Id, DayOfWeek = day, StartMinute = 540, EndMinute = 660 });
            await db.SaveChangesAsync(Ct);
        });
        var turn = await Send(h, user.Id, $"Xếp lại lịch {label}");
        Assert.Equal("PREVIEW", turn.Status);
        var monday = PlanningEngine.At(new DateOnly(2026, 10, 19).AddDays(offset), 0, PlanningEngine.Zone("Asia/Ho_Chi_Minh"));
        Assert.Equal(monday.AddDays(7), turn.Plan!.End);
        Assert.All(turn.Plan.Sessions, session => Assert.True(session.Start >= (monday > h.Clock.GetUtcNow() ? monday : h.Clock.GetUtcNow()) && session.End <= monday.AddDays(7)));
    }

    [Fact]
    public async Task Optimization_cannot_be_mixed_with_invented_task_or_deadline_edits()
    {
        var ai = OptimizationAi(); ai.Response = _ => AgentAiTestDouble.Empty() with { Reschedule = true, OptimizationQuery = Query,
            Tasks = [new(null, "Bịa thêm", At(1320), 60, 2, "OTHER", true)] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var error = await Assert.ThrowsAsync<AuthException>(() => Send(h, user.Id, "Tối ưu lịch ngày mai"));
        Assert.Equal("AGENT_INVALID_INTENT", error.Code);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
    }
}
