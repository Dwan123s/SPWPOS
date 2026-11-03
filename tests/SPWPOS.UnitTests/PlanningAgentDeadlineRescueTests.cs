using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Increasing_important_near_deadline_report_to_four_hours_proposes_later_work_moves_and_supports_undo(bool forbidMoves)
    {
        var ai = new AgentAiTestDouble();
        await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        DateTimeOffset At(int days, int hour, int minute = 0) => new DateTimeOffset(now.UtcDateTime.Date.AddDays(days), TimeSpan.Zero).AddHours(hour).AddMinutes(minute);
        var report = new WorkTask { UserId = user.Id, Name = "Report SWP", EstimatedMinutes = 120,
            Deadline = At(1, 12), IsHardDeadline = true, ImportanceLevel = "Important", Priority = 2 };
        var later = new WorkTask { UserId = user.Id, Name = "Report SSG105", EstimatedMinutes = 120, Deadline = At(4, 12), IsHardDeadline = true };
        var oldLater = new WorkSession { UserId = user.Id, TaskId = later.Id, Start = At(0, 18, 30), End = At(0, 20, 30) };
        var original = new[] { oldLater,
            new WorkSession { UserId = user.Id, TaskId = report.Id, Start = At(0, 20, 30), End = At(0, 21, 30) },
            new WorkSession { UserId = user.Id, TaskId = report.Id, Start = At(1, 11), End = At(1, 12) } };
        await h.DbAsync(async db => {
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.Where(x => x.UserId == user.Id).ToArrayAsync(Ct));
            db.WeeklyAvailability.AddRange(new WeeklyAvailability { UserId = user.Id, DayOfWeek = (int)now.DayOfWeek, StartMinute = 1110, EndMinute = 1320 },
                new WeeklyAvailability { UserId = user.Id, DayOfWeek = (int)now.AddDays(1).DayOfWeek, StartMinute = 480, EndMinute = 720 },
                new WeeklyAvailability { UserId = user.Id, DayOfWeek = (int)now.AddDays(2).DayOfWeek, StartMinute = 480, EndMinute = 1080 });
            db.WorkTasks.AddRange(report, later); db.WorkSessions.AddRange(original);
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Morning class", Start = At(1, 8), End = At(1, 11) });
            await db.SaveChangesAsync(Ct);
        });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(report.Id, report.Name, null, 240, null, "OTHER", null)] };
        var text = $"Cập nhật công việc {report.Name} (mã {report.Id}) thành 4 tiếng." + (forbidMoves ? " Không dời các task khác." : "");
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(text), Ct));
        if (forbidMoves)
        {
            Assert.Equal("BLOCKED", turn.Status);
            Assert.Equal(120, await h.DbAsync(db => db.WorkTasks.Where(t => t.Id == report.Id).Select(t => t.EstimatedMinutes).SingleAsync(Ct)));
            Assert.All(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)), s => Assert.Equal(WorkSessionStatus.Scheduled, s.Status));
            return;
        }
        Assert.Equal("PREVIEW", turn.Status); Assert.True(turn.Plan!.HasUrgentReplan); Assert.True(turn.Plan.RequiresConfirmation);
        Assert.Contains(turn.Changes, c => c.Kind == "SESSION" && c.TaskId == later.Id && c.Before != c.After);
        Assert.Equal(120, await h.DbAsync(db => db.WorkTasks.Where(t => t.Id == report.Id).Select(t => t.EstimatedMinutes).SingleAsync(Ct)));
        var applied = await Agent(h, s => s.ApplyAgentTurnAsync(user.Id, turn.Id, Ct));
        Assert.Equal("APPLIED", applied.Status);
        Assert.Equal(240, await h.DbAsync(db => db.WorkTasks.Where(t => t.Id == report.Id).Select(t => t.EstimatedMinutes).SingleAsync(Ct)));
        var active = await h.DbAsync(db => db.WorkSessions.Where(s => s.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
        Assert.Equal(240, active.Where(s => s.TaskId == report.Id).Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.All(active.Where(s => s.TaskId == report.Id), s => Assert.True(s.End <= report.Deadline));
        Assert.Equal(120, active.Where(s => s.TaskId == later.Id).Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.All(active.Where(s => s.TaskId == later.Id), s => Assert.True(s.End <= later.Deadline));
        await Agent(h, s => s.UndoAgentTurnAsync(user.Id, turn.Id, Ct));
        Assert.Equal(120, await h.DbAsync(db => db.WorkTasks.Where(t => t.Id == report.Id).Select(t => t.EstimatedMinutes).SingleAsync(Ct)));
        var restored = await h.DbAsync(db => db.WorkSessions.Where(s => s.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
        Assert.Equal(original.Length, restored.Length);
        Assert.All(original, old => Assert.Contains(restored, s => s.Id == old.Id && s.Start == old.Start && s.End == old.End));
    }
}
