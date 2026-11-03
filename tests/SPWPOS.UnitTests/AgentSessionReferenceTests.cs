using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    [Theory]
    [InlineData("Dời phiên làm việc 12:45–14:05 để kiểm tra lúc 14:00")]
    [InlineData("Chuyển phiên 12:45-14:05 để đi họp lúc 14:00")]
    [InlineData("Lùi buổi làm việc 12h45 đến 14h05 để đi khám lúc 14:00")]
    [InlineData("Không dời lịch kiểm tra. Dời phiên làm việc 12:45–14:05 để kiểm tra lúc 14:00")]
    public async Task Clock_referenced_work_moves_while_requested_appointment_and_unrelated_work_stay_fixed(string request)
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h, user.Id);
        var day = new DateTimeOffset(h.Clock.GetUtcNow().Date.AddDays(1), TimeSpan.Zero);
        var task = new WorkTask { UserId = user.Id, Name = "Phần việc có sẵn", EstimatedMinutes = 80, Deadline = day.AddHours(22) };
        var other = new WorkTask { UserId = user.Id, Name = "Công việc khác", EstimatedMinutes = 60, Deadline = day.AddDays(1).AddHours(22) };
        var a = new WorkSession { UserId = user.Id, TaskId = task.Id, Start = day.AddHours(12).AddMinutes(45), End = day.AddHours(14).AddMinutes(5) };
        var b = new WorkSession { UserId = user.Id, TaskId = other.Id, Start = day.AddHours(17), End = day.AddHours(18) };
        await h.DbAsync(async db => { db.WorkTasks.AddRange(task, other); db.WorkSessions.AddRange(a,b); await db.SaveChangesAsync(Ct); });
        ai.Response = _ => AgentAiTestDouble.Empty() with { Events = [new("Kiểm tra", day.AddHours(14), day.AddHours(15), EventType: "APPOINTMENT")], ScheduleTaskIds = [task.Id,other.Id], Reschedule = true };
        var result = await Agent(h, s => s.SendAgentTurnAsync(user.Id, Request(request + " ngày mai, kết thúc 15:00."), Ct));
        Assert.Contains(result.Status, new[] { "APPLIED", "PREVIEW" }); Assert.NotNull(result.Plan);
        Assert.Contains(result.Plan.Sessions, s => s.Id == b.Id && s.Preserved && s.Start == b.Start && s.End == b.End);
        Assert.All(result.Plan.Sessions.Where(s => s.TaskId == task.Id), s => Assert.True(s.End <= day.AddHours(14) || s.Start >= day.AddHours(15)));
        Assert.Equal(80, result.Plan.Sessions.Where(s => s.TaskId == task.Id).Sum(s => (s.End-s.Start).TotalMinutes));
        Assert.Contains(result.Changes, c => c.Kind == "EVENT" && c.After.Contains("14:00"));
        Assert.Equal(task.Deadline, (await h.DbAsync(db => db.WorkTasks.SingleAsync(t=>t.Id==task.Id,Ct))).Deadline);
    }
    [Fact] public async Task A_negated_clock_reference_does_not_authorize_moving_work()
    {
        var ai = new AgentAiTestDouble(); await using var h = await Harness(ai); var user = await h.RegisterAsync(); await Setup(h,user.Id);
        var day = new DateTimeOffset(h.Clock.GetUtcNow().Date.AddDays(1),TimeSpan.Zero);
        var task = new WorkTask {UserId=user.Id,Name="Việc giữ nguyên",EstimatedMinutes=80,Deadline=day.AddHours(22)};
        var session = new WorkSession {UserId=user.Id,TaskId=task.Id,Start=day.AddHours(12).AddMinutes(45),End=day.AddHours(14).AddMinutes(5)};
        await h.DbAsync(async db=>{db.WorkTasks.Add(task);db.WorkSessions.Add(session);await db.SaveChangesAsync(Ct);});
        ai.Response=_=>AgentAiTestDouble.Empty() with {Events=[new("Kiểm tra",day.AddHours(14),day.AddHours(15),EventType:"APPOINTMENT")]};
        await Assert.ThrowsAsync<AuthException>(()=>Agent(h,s=>s.SendAgentTurnAsync(user.Id,Request("Không dời phiên làm việc 12:45–14:05. Thêm kiểm tra ngày mai 14:00–15:00."),Ct)));
        Assert.Equal(session.Start,(await h.DbAsync(db=>db.WorkSessions.SingleAsync(Ct))).Start);
        Assert.Empty(await h.DbAsync(db=>db.FixedEvents.ToArrayAsync(Ct)));
    }
}
