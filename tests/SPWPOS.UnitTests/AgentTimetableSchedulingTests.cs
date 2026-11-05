using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class AgentFreeTimeTests
{
    [Fact]
    public async Task Combined_timetables_replan_flexible_work_around_busy_hours_from_both_images()
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [
            new("Lịch học", At(840), At(900), EventType: "CLASS"),
            new("Lịch làm", At(900), At(960), EventType: "WORK")] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var task = new WorkTask { UserId = user.Id, Name = "Báo cáo", Deadline = At(1020), EstimatedMinutes = 60, Splittable = false, IsHardDeadline = true };
        await h.DbAsync(async db => {
            var availability = await db.WeeklyAvailability.SingleAsync(Ct); availability.StartMinute = 840; availability.EndMinute = 1020;
            db.WorkTasks.Add(task);
            db.WorkSessions.Add(new() { UserId = user.Id, TaskId = task.Id, Start = At(840), End = At(900) });
            await db.SaveChangesAsync(Ct);
        });
        var image = new AgentImage("Lịch học.png", "image/png", "iVBORw0KGgo=", AdditionalImages: [new("Lịch làm.png", "image/png", "iVBORw0KGgo=")]);
        var result = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), "Gộp hai thời khóa biểu và xếp lịch quanh giờ bận", Image: image), Ct));
        Assert.Equal("APPLIED", result.Status);
        Assert.Equal(2, ai.LastImage!.GetImages().Length);
        Assert.Equal(2, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        var session = await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Status == WorkSessionStatus.Scheduled, Ct));
        Assert.Equal(At(960), session.Start); Assert.Equal(At(1020), session.End);
    }
    [Theory] [InlineData("flexible")] [InlineData("locked")] [InlineData("fixed")]
    public async Task An_uploaded_timetable_keeps_class_hours_and_replans_only_flexible_work(string mode)
    {
        var ai = Ai(); ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Toán", At(840), At(900), EventType: "CLASS")] };
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var task = new WorkTask { UserId = user.Id, Name = "Bài tập", Deadline = At(1020), EstimatedMinutes = 60, Splittable = false, IsHardDeadline = true };
        var session = new WorkSession { UserId = user.Id, TaskId = task.Id, Start = At(840), End = At(900), IsLocked = mode == "locked" };
        await h.DbAsync(async db => {
            var availability = await db.WeeklyAvailability.SingleAsync(Ct); availability.StartMinute = 840; availability.EndMinute = 1020;
            if (mode == "fixed") db.FixedEvents.Add(new() { UserId = user.Id, Name = "Hẹn khám", EventType = "APPOINTMENT", Start = At(840), End = At(900) });
            else { db.WorkTasks.Add(task); db.WorkSessions.Add(session); }
            await db.SaveChangesAsync(Ct);
        });
        var input = new AgentTurnInput(Guid.NewGuid(), Guid.NewGuid(),
            "Tạo lịch học. Thời khóa biểu từ ảnh (dữ liệu để đối chiếu): Toán, ngày mai 14:00–15:00, chỉ một lần.", true);
        if (mode != "flexible") {
            var error = await Assert.ThrowsAsync<AuthException>(() => UseAgent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct)));
            Assert.Equal(mode == "locked" ? "LOCKED_SESSION_CONFLICT" : "FIXED_EVENT_OVERLAP", error.Code);
            Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(x => x.EventType == "CLASS", Ct)));
            return;
        }
        var result = await UseAgent(h, s => s.SendAgentTurnAsync(user.Id, input, Ct));
        Assert.Equal("APPLIED", result.Status);
        var savedClass = await h.DbAsync(db => db.FixedEvents.SingleAsync(Ct));
        Assert.Equal(At(840), savedClass.Start); Assert.Equal(At(900), savedClass.End);
        Assert.False(savedClass.IsUrgent ?? false);
        var moved = await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Status == WorkSessionStatus.Scheduled, Ct));
        Assert.Equal(At(900), moved.Start); Assert.Equal(At(960), moved.End);
        Assert.Equal(task.Deadline, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).Deadline);
        await UseAgent(h, s => s.UndoAgentTurnAsync(user.Id, result.Id, Ct));
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(Ct)));
        Assert.Equal(At(840), (await h.DbAsync(db => db.WorkSessions.SingleAsync(x => x.Status == WorkSessionStatus.Scheduled, Ct))).Start);
    }
}
