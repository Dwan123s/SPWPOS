using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class AnalyticsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    [Fact] public async Task Focus_is_split_at_local_midnight_and_completion_uses_actual_date()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        h.Clock.Advance(DateTimeOffset.Parse("2026-10-16T03:00:00Z") - h.Clock.GetUtcNow());
        var start = DateTimeOffset.Parse("2026-10-15T16:30:00Z"); // 23:30 in VN
        var task = new WorkTask { UserId = user.Id, Name = "Report", EstimatedMinutes = 60, Status = WorkTaskStatus.Completed,
            Deadline = start.AddHours(2), CompletedAt = start.AddHours(1), CreatedAt = start.AddDays(-1) };
        var session = new WorkSession { UserId = user.Id, TaskId = task.Id, Start = start.AddHours(-2), End = start.AddHours(-1), Status = WorkSessionStatus.Completed };
        var execution = new SessionExecution { UserId = user.Id, SessionId = session.Id, StartedAt = start, EndedAt = start.AddHours(1), ActualSeconds = 7200, IsUserAdjusted = true };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); db.WorkSessions.Add(session); db.SessionExecutions.Add(execution);
            db.ExecutionSegments.Add(new() { UserId=user.Id, ExecutionId=execution.Id, Start=start, End=start.AddHours(1) }); await db.SaveChangesAsync(Ct); });
        var result = await Work(h, s => s.GetAnalyticsAsync(user.Id, new(new(2026,10,15), new(2026,10,16)), Ct));
        Assert.Equal(new long[] {1800,1800},result.Days.Select(x=>x.FocusSeconds));
        Assert.Equal(new[] {0,1},result.Days.Select(x=>x.CompletedTasks));
        Assert.Equal(3600,result.Current.FocusSeconds); Assert.Equal(7200,result.Current.RecordedActualSeconds);
        Assert.Contains("MANUAL_ACTUAL_SEPARATE_FROM_FOCUS",result.Notices); Assert.Null(result.FocusChangePercent);
    }
    [Fact] public async Task Open_segments_are_provisional_and_other_owners_are_excluded()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var now = h.Clock.GetUtcNow(); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now,"Asia/Ho_Chi_Minh").DateTime);
        var task = new WorkTask { UserId=user.Id, Name="Running", Deadline=now.AddDays(1) };
        var session = new WorkSession { UserId=user.Id,TaskId=task.Id,Start=now.AddMinutes(-1),End=now.AddMinutes(59),Status=WorkSessionStatus.Running };
        var execution = new SessionExecution { UserId=user.Id,SessionId=session.Id,StartedAt=now.AddMinutes(-1) };
        await h.DbAsync(async db=>{db.WorkTasks.Add(task);db.WorkSessions.Add(session);db.SessionExecutions.Add(execution);
            db.ExecutionSegments.Add(new(){UserId=user.Id,ExecutionId=execution.Id,Start=now.AddMinutes(-1)});
            var other = new ApplicationUser { Id=Guid.NewGuid(),UserName="other-analytics",NormalizedUserName="OTHER-ANALYTICS",Email="other-analytics@example.com",NormalizedEmail="OTHER-ANALYTICS@EXAMPLE.COM" }; db.Users.Add(other);
            db.WorkTasks.Add(new(){UserId=other.Id,Name="Other owner",Status=WorkTaskStatus.Completed,CompletedAt=now,Deadline=now.AddDays(1)});
            await db.SaveChangesAsync(Ct);});
        var result=await Work(h,s=>s.GetAnalyticsAsync(user.Id,new(today,today),Ct));
        Assert.Equal(0,result.Current.FocusSeconds);Assert.Equal(60,result.Current.OpenFocusSeconds);Assert.Equal(0,result.Current.CompletedTasks);
        var tasks=await Work(h,s=>s.GetTaskPerformanceAsync(user.Id,1,20,null,Ct));Assert.Equal(1,tasks.Total);Assert.Equal("Running",Assert.Single(tasks.Items).Name);
    }
    [Fact] public async Task Task_history_has_real_pagination_beyond_eight_rows()
    {
        await using var h=await ServiceHarness.CreateAsync();var user=await h.RegisterAsync();
        await h.DbAsync(async db=>{db.WorkTasks.AddRange(Enumerable.Range(0,25).Select(i=>new WorkTask {UserId=user.Id,Name=$"Task {i}",CreatedAt=h.Clock.GetUtcNow().AddMinutes(i),Deadline=h.Clock.GetUtcNow().AddDays(1)}));await db.SaveChangesAsync(Ct);});
        var first=await Work(h,s=>s.GetTaskPerformanceAsync(user.Id,1,20,null,Ct));var second=await Work(h,s=>s.GetTaskPerformanceAsync(user.Id,2,20,null,Ct));
        Assert.Equal(25,first.Total);Assert.Equal(20,first.Items.Length);Assert.Equal(5,second.Items.Length);Assert.Empty(first.Items.Select(x=>x.Id).Intersect(second.Items.Select(x=>x.Id)));
    }
}
