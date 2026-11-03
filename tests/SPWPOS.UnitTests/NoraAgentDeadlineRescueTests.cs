using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed partial class NoraAgentRuntimeTests
{
    [Fact] public async Task Native_keep_existing_proposes_deadline_rescue_without_replanning_permission_or_extra_ai_rounds()
    {
        var model = new Model();
        await using var h = await Harness(model);
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-03T16:30:00Z") - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var now = h.Clock.GetUtcNow();
        DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, TimeSpan.Zero);
        var report = new WorkTask { UserId = user.Id, Name = "Report SWP", EstimatedMinutes = 120,
            Deadline = At(4, 12), IsHardDeadline = true, ImportanceLevel = "Important", Priority = 2 };
        var later = new WorkTask { UserId = user.Id, Name = "Report SSG105", EstimatedMinutes = 120, Deadline = At(7, 12), IsHardDeadline = true };
        var old = new WorkSession { UserId = user.Id, TaskId = later.Id, Start = At(3, 18, 30), End = At(3, 20, 30) };
        await h.DbAsync(async db => {
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.Where(x => x.UserId == user.Id).ToArrayAsync(Ct));
            db.WeeklyAvailability.AddRange(new WeeklyAvailability { UserId = user.Id, DayOfWeek = 2, StartMinute = 1110, EndMinute = 1320 },
                new WeeklyAvailability { UserId = user.Id, DayOfWeek = 3, StartMinute = 480, EndMinute = 720 },
                new WeeklyAvailability { UserId = user.Id, DayOfWeek = 4, StartMinute = 480, EndMinute = 1080 });
            db.WorkTasks.AddRange(report, later); db.WorkSessions.Add(old);
            db.FixedEvents.Add(new() { UserId = user.Id, Name = "Morning class", Start = At(4, 8), End = At(4, 11) });
            await db.SaveChangesAsync(Ct);
        });
        var draft = JsonNode.Parse(Draft)!; var task = draft["tasks"]![0]!;
        task["taskId"] = report.Id.ToString(); task["name"] = report.Name;
        task["deadline"] = report.Deadline.ToString("O"); task["estimatedMinutes"] = 240;
        task["splittable"] = true; task["durationEvidence"] = "4 tiếng"; task["deadlineEvidence"] = "";
        draft["horizonDays"] = 7;
        model.Decision = (request, n) => {
            if (n == 1) return Task.FromResult(Call("get_tasks", new { query = report.Id.ToString() }, n));
            var observation = request.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("response");
            if (n != 2) throw new InvalidOperationException(observation.GetRawText());
            var data = observation.GetProperty("data");
            Assert.True(data.GetProperty("tasks")[0].TryGetProperty("importance", out var importance), data.GetRawText());
            Assert.Equal("Important", importance.GetProperty("Level").GetString());
            return Task.FromResult(Call("propose_changes", new { intent = JsonSerializer.SerializeToElement(draft), strategy = "keep_existing" }, n));
        };
        var turn = await Agent(h, s => s.SendAgentTurnAsync(user.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), $"Cập nhật công việc {report.Name} (mã {report.Id}) thành 4 tiếng."), Ct));
        Assert.Equal("PREVIEW", turn.Status); Assert.Equal(2, model.Calls);
        Assert.True(turn.Plan!.RequiresConfirmation); Assert.True(turn.Plan.HasUrgentReplan);
        Assert.Empty(turn.Plan.Warnings);
        Assert.Equal(240, turn.Plan.Sessions.Where(s => s.TaskId == report.Id).Sum(s => (s.End - s.Start).TotalMinutes));
        Assert.Contains(turn.Changes, c => c.Kind == "SESSION" && c.TaskId == later.Id && c.Before != c.After);
        Assert.Equal(120, await h.DbAsync(db => db.WorkTasks.Where(t => t.Id == report.Id).Select(t => t.EstimatedMinutes).SingleAsync(Ct)));
        Assert.Equal(WorkSessionStatus.Scheduled, await h.DbAsync(db => db.WorkSessions.Where(s => s.Id == old.Id).Select(s => s.Status).SingleAsync(Ct)));
    }
}
