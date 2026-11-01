using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using System.Text.Json;
using SPWPOS.Application.Authentication;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact]
    public async Task Latest_import_selects_its_monday_to_sunday_week_even_when_older_calendar_events_exist()
    {
        await using var h = await Setup(hours: false);
        var first = new DateOnly(2026, 11, 9); var zone = PlanningEngine.Zone("Asia/Ho_Chi_Minh");
        var entry = new FixedEvent { UserId = Member, Name = "Thời khóa biểu mới", Start = PlanningEngine.At(first.AddDays(2), 480, zone), End = PlanningEngine.At(first.AddDays(2), 600, zone) };
        var turn = new AgentTurn { UserId = Member, ConversationId = Guid.NewGuid(), RequestId = Guid.NewGuid(), Status = "APPLIED", CreatedAt = h.Clock.GetUtcNow(), UpdatedAt = h.Clock.GetUtcNow(),
            ChangesJson = JsonSerializer.Serialize(new { analyzeTimetable = true, events = new[] { entry } }) };
        await h.DbAsync(async db => { db.FixedEvents.Add(entry); db.AgentTurns.Add(turn); await db.SaveChangesAsync(Ct); });
        var preview = await Use(h, s => s.PreviewAvailabilityPolicyAsync(Member, Ct));
        Assert.Equal(first, preview.WeekStart); Assert.Equal(first.AddDays(6), preview.WeekEnd);
        Assert.Equal(Enumerable.Range(0, 7).Select(i => first.AddDays(i)), preview.Days!.Select(x => x.Date));
        Assert.Contains(preview.Days![2].Timeline!, x => x.Kind == "FIXED" && x.Label == entry.Name);
        Assert.Empty(await h.DbAsync(db => db.AvailabilityPolicies.ToArrayAsync(Ct)));
        await Use(h, s => s.ConfirmAvailabilityPolicyAsync(Member, preview.Id, Ct));
        var policy = await h.DbAsync(db => db.AvailabilityPolicies.AsNoTracking().SingleAsync(x => x.UserId == Member, Ct));
        Assert.Equal(7, JsonSerializer.Deserialize<RoutineDay[]>(policy.RoutineDaysJson)!.Length);
        Assert.False(policy.AppliesOn(first.AddDays(-1))); Assert.False(policy.AppliesOn(first.AddDays(7)));
        var noHoursOutside = new PlanningState([], [], [], [], [], [], new() { TimeZoneId = "Asia/Ho_Chi_Minh" }, Policy: policy);
        Assert.Empty(new PlanningEngine().FreeSlots(noHoursOutside, PlanningEngine.At(first.AddDays(7), 0, zone), PlanningEngine.At(first.AddDays(8), 0, zone)));
        Assert.Equal(first, (await Use(h, s => s.GetPolicySlotsAsync(Member, Ct)))[0].Date);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.PreviewAvailabilityPolicyAsync(Other, Ct, turn.Id)));
    }

    [Fact]
    public async Task A_new_week_proposal_retains_previously_confirmed_days_and_sessions_outside_its_week()
    {
        await using var h = await Setup(hours: false);
        var first = new DateOnly(2026, 11, 2); var zone = PlanningEngine.Zone("Asia/Ho_Chi_Minh");
        var a = await Use(h, s => s.PreviewAvailabilityPolicyAsync(Member, Ct));
        await Use(h, s => s.ConfirmAvailabilityPolicyAsync(Member, a.Id, Ct));
        var before = await h.DbAsync(db => db.AvailabilityPolicies.AsNoTracking().SingleAsync(x => x.UserId == Member, Ct));
        var task = new WorkTask { UserId = Member, Name = "Giữ lịch tuần trước", Deadline = PlanningEngine.At(first.AddDays(4), 1200, zone), EstimatedMinutes = 60 };
        var session = new WorkSession { UserId = Member, TaskId = task.Id, Start = PlanningEngine.At(first.AddDays(2), 600, zone), End = PlanningEngine.At(first.AddDays(2), 660, zone) };
        var next = first.AddDays(7);
        var entry = new FixedEvent { UserId = Member, Name = "Tuần tiếp", Start = PlanningEngine.At(next, 600, zone), End = PlanningEngine.At(next, 660, zone) };
        var turn = new AgentTurn { UserId = Member, ConversationId = Guid.NewGuid(), RequestId = Guid.NewGuid(), Status = "APPLIED", CreatedAt = h.Clock.GetUtcNow(), UpdatedAt = h.Clock.GetUtcNow(),
            ChangesJson = JsonSerializer.Serialize(new { analyzeTimetable = true, events = new[] { entry } }) };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session); db.FixedEvents.Add(entry); db.AgentTurns.Add(turn); await db.SaveChangesAsync(Ct); });
        var b = await Use(h, s => s.PreviewAvailabilityPolicyAsync(Member, Ct, turn.Id));
        Assert.Equal(next, b.WeekStart); Assert.Equal(7, b.Days!.Length);
        Assert.DoesNotContain(b.Before, s => s.Id == session.Id); Assert.DoesNotContain(b.After, s => s.Id == session.Id);
        await Use(h, s => s.ConfirmAvailabilityPolicyAsync(Member, b.Id, Ct));
        var after = await h.DbAsync(db => db.AvailabilityPolicies.AsNoTracking().SingleAsync(x => x.UserId == Member, Ct));
        Assert.Equal(before.ForDate(first), after.ForDate(first)); Assert.True(after.AppliesOn(next)); Assert.False(after.AppliesOn(next.AddDays(7)));
        var retained = await h.DbAsync(db => db.WorkSessions.AsNoTracking().SingleAsync(x => x.Id == session.Id, Ct));
        Assert.Equal(WorkSessionStatus.Scheduled, retained.Status); Assert.Equal(session.Start, retained.Start); Assert.Equal(session.End, retained.End);
    }

    [Fact]
    public async Task Current_week_keeps_monday_to_sunday_without_making_past_time_schedulable()
    {
        await using var h = await Setup(hours: false);
        h.Clock.Advance(TimeSpan.FromDays(4));
        var preview = await Use(h, s => s.PreviewAvailabilityPolicyAsync(Member, Ct));
        var monday = new DateOnly(2026, 11, 2);
        Assert.Equal(monday, preview.WeekStart); Assert.Equal(monday.AddDays(6), preview.WeekEnd);
        Assert.All(preview.Days!.SelectMany(d => d.WorkGaps), slot => Assert.True(slot.Start >= h.Clock.GetUtcNow()));
        Assert.Empty(preview.Days![0].WorkGaps);
    }
    [Fact]
    public async Task Timetable_produces_sleep_meals_rest_and_free_time_without_manual_hours()
    {
        await using var h = await Setup(hours:false);
        var day = new DateOnly(2026,11,2);
        DateTimeOffset At(int minute) => PlanningEngine.At(day,minute,PlanningEngine.Zone("Asia/Ho_Chi_Minh"));
        await h.DbAsync(async db => {
            db.FixedEvents.RemoveRange(await db.FixedEvents.Where(x=>x.UserId==Member).ToArrayAsync(Ct));
            db.FixedEvents.AddRange(new FixedEvent { UserId=Member,Name="Môn 1",Start=At(420),End=At(555) },new FixedEvent { UserId=Member,Name="Môn 2",Start=At(570),End=At(705) }); await db.SaveChangesAsync(Ct);
        });
        var preview = await Use(h,s=>s.PreviewAvailabilityPolicyAsync(Member,Ct));
        Assert.Empty(preview.Warnings); var monday=Assert.Single(preview.Days!,d=>d.Date==day);
        Assert.Equal(day, preview.WeekStart); Assert.Equal(day.AddDays(6), preview.WeekEnd); Assert.Equal(7, preview.Days!.Length);
        Assert.Equal(360,monday.Routine!.AwakeStart);
        Assert.Equal(735,monday.Routine.LunchStart); Assert.Equal(765,monday.Routine.LunchEnd);
        Assert.Equal(30,monday.Routine.DinnerEnd-monday.Routine.DinnerStart); Assert.Equal(15,monday.Routine.RestEnd-monday.Routine.RestStart);
        Assert.Contains(monday.WorkGaps,s=>s.Start==At(765));
        Assert.DoesNotContain(monday.WorkGaps,s=>s.Start<At(705)&&s.End>At(420));
        Assert.Contains(monday.Timeline!,b=>b.Kind=="COMMUTE"); Assert.Contains(monday.Timeline!,b=>b.Kind=="SLEEP"); Assert.Contains(monday.Timeline!,b=>b.Kind=="FREE");
        Assert.Equal(0,await h.DbAsync(db=>db.AvailabilityPolicies.CountAsync(Ct))); Assert.Equal(0,await h.DbAsync(db=>db.WeeklyAvailability.CountAsync(Ct)));
        await Use(h,s=>s.ConfirmAvailabilityPolicyAsync(Member,preview.Id,Ct));
        var policy=await h.DbAsync(db=>db.AvailabilityPolicies.AsNoTracking().SingleAsync(x=>x.UserId==Member,Ct)); Assert.Equal(735,policy.ForDate(day)!.LunchStart);
        await Workspace(h,async s=>{await s.SaveExceptionAsync(Member,null,new(day,780,840,false,"Hoạt động extra"),Ct);return true;});
        var updated=Assert.Single(await Use(h,s=>s.GetPolicySlotsAsync(Member,Ct)),d=>d.Date==day);
        Assert.DoesNotContain(updated.WorkGaps,s=>s.Start<At(840)&&s.End>At(780));
        Assert.Contains(updated.Timeline!,b=>b.Kind=="EXTRA"&&b.Label=="Hoạt động extra");
    }
}
