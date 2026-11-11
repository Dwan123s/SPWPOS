using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;
public sealed class WorkTimeApiTests
{
    [Fact]
    public async Task Nora_today_advice_matches_workload_with_ai_disabled_and_does_not_change_the_plan()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory, client);
        var before = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/intelligence/workload")).GetProperty("data");
        var reply = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/agent/turns",
            new { conversationId = Guid.NewGuid(), requestId = Guid.NewGuid(), text = "Hôm nay nên làm gì?", autoApply = true })).GetProperty("data");
        Assert.Equal("ANSWERED", reply.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("next").GetProperty("reason").GetString(), reply.GetProperty("reply").GetString());
        var after = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/intelligence/workload")).GetProperty("data");
        Assert.Equal(before.GetProperty("revision").GetGuid(), after.GetProperty("revision").GetGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlanProposals.ToArrayAsync(ct));
    }

    [Fact]
    public async Task Partial_outcome_is_replayable_and_does_not_create_recovery_sessions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        var tokens = await AuthFlowTests.RegisterLoginAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        var now = factory.Clock.GetUtcNow();
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uid = await db.Users.Select(u => u.Id).SingleAsync(ct);
            var task = new WorkTask { UserId = uid, Name = "Report", EstimatedMinutes = 300, Deadline = now.AddDays(1) };
            var session = new WorkSession { UserId = uid, TaskId = task.Id, Start = now.AddMinutes(-121),
                End = now.AddMinutes(-1), Status = WorkSessionStatus.NeedsReview };
            id = session.Id;
            db.AddRange(task, session);
            await db.SaveChangesAsync(ct);
        }
        var path = $"/api/sessions/{id}/outcome";
        var input = new { outcome = "PARTIAL", actualMinutes = 60, remainingMinutes = 90 };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, input, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, input, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { outcome = "MISSED" }, ct)).StatusCode);
        using var workload = JsonDocument.Parse(await client.GetStringAsync("/api/intelligence/workload", ct));
        var days = workload.RootElement.GetProperty("data").GetProperty("days").EnumerateArray().ToArray();
        Assert.Equal(7, days.Length);
        var day = Assert.Single(days, day => day.GetProperty("sessions").GetArrayLength() > 0);
        Assert.Equal(3600L, day.GetProperty("recordedActualSeconds").GetInt64());
        Assert.Equal(3600L, Assert.Single(day.GetProperty("sessions").EnumerateArray()).GetProperty("actualSeconds").GetInt64());
        Assert.Contains(days, day => day.GetProperty("recordedActualSeconds").ValueKind == JsonValueKind.Null);
        await using var check = factory.Services.CreateAsyncScope();
        var persisted = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3600L, (await persisted.SessionExecutions.SingleAsync(ct)).ActualSeconds);
        Assert.Equal(id, (await persisted.WorkSessions.SingleAsync(ct)).Id);
        Assert.NotEqual(WorkTaskStatus.Completed, (await persisted.WorkTasks.SingleAsync(ct)).Status);
    }

    [Fact]
    public async Task Workload_splits_midnight_plans_and_attributes_recorded_zero_to_execution_end_day()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory, client);
        factory.Clock.Advance(DateTimeOffset.Parse("2026-11-12T01:00:00Z") - factory.Clock.GetUtcNow());
        var now = factory.Clock.GetUtcNow();
        Guid sessionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uid = await db.Users.Select(u => u.Id).SingleAsync(ct);
            db.WorkPreferences.Add(new() { UserId = uid, TimeZoneId = "UTC" });
            var task = new WorkTask { UserId = uid, Name = "Midnight report", Deadline = now.AddDays(1) };
            var session = new WorkSession { UserId = uid, TaskId = task.Id,
                Start = now.AddMinutes(-90), End = now.AddMinutes(-30) };
            sessionId = session.Id;
            db.AddRange(task, session);
            await db.SaveChangesAsync(ct);
        }
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/sessions/{sessionId}/outcome",
            new SessionOutcomeInput("COMPLETED", 0));
        var workload = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/intelligence/workload")).GetProperty("data");
        var days = workload.GetProperty("days").EnumerateArray().ToArray();
        Assert.Equal("2026-11-09", workload.GetProperty("start").GetString());
        var wednesday = days.Single(d => d.GetProperty("date").GetString() == "2026-11-11");
        var thursday = days.Single(d => d.GetProperty("date").GetString() == "2026-11-12");
        Assert.Equal(1800, wednesday.GetProperty("plannedSeconds").GetInt64());
        Assert.Equal(1800, thursday.GetProperty("plannedSeconds").GetInt64());
        Assert.Equal(JsonValueKind.Null, wednesday.GetProperty("recordedActualSeconds").ValueKind);
        Assert.Equal(0, thursday.GetProperty("recordedActualSeconds").GetInt64());
        Assert.Equal(0, workload.GetProperty("completedTasks").GetInt32());
    }

    [Theory]
    [InlineData(false, false, 90, 0)]
    [InlineData(true, false, 30, 0)]
    [InlineData(false, true, 60, 30)]
    public async Task Strict_recovery_uses_confirmed_remaining_and_preserves_existing_coverage(
        bool hasCoverage, bool limited, int planned, int shortfall)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory();
        using var client = await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory, client);
        factory.Clock.Advance(DateTimeOffset.Parse("2026-11-11T09:00:00Z") - factory.Clock.GetUtcNow());
        var now = factory.Clock.GetUtcNow();
        Guid taskId, sessionId;
        Guid? coverageId = null;
        var deadline = now.AddHours(6);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uid = await db.Users.Select(u => u.Id).SingleAsync(ct);
            db.WorkPreferences.Add(new() { UserId = uid, TimeZoneId = "UTC", BreakMinutes = 0,
                MinimumSessionMinutes = 25, MaximumSessionMinutes = 120, MaximumDailyMinutes = 1440 });
            db.WeeklyAvailability.Add(new() { UserId = uid, DayOfWeek = (int)now.DayOfWeek,
                StartMinute = 10 * 60, EndMinute = (limited ? 11 : 14) * 60 });
            var task = new WorkTask { UserId = uid, Name = "Five-hour report", EstimatedMinutes = 300,
                Deadline = deadline, IsHardDeadline = true, Splittable = true };
            var ended = new WorkSession { UserId = uid, TaskId = task.Id, Start = now.AddDays(-1).AddHours(-2), End = now.AddDays(-1),
                Status = WorkSessionStatus.NeedsReview };
            (taskId, sessionId) = (task.Id, ended.Id);
            db.AddRange(task, ended);
            if (hasCoverage)
            {
                var coverage = new WorkSession { UserId = uid, TaskId = taskId, Start = now.AddHours(2), End = now.AddHours(3) };
                coverageId = coverage.Id;
                db.Add(coverage);
            }
            await db.SaveChangesAsync(ct);
        }
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/sessions/{sessionId}/outcome",
            new SessionOutcomeInput("PARTIAL", 60, 90));
        var plan = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans/reschedule",
            new PlanInput(now, 30, [taskId], UseCapacityFactor: false, UseEstimateBias: false) { Strict = true, PreserveExisting = true }))
            .GetProperty("data");
        var proposed = plan.GetProperty("sessions").EnumerateArray().ToArray();
        Assert.Equal(planned, proposed.Where(s => !s.GetProperty("preserved").GetBoolean()).Sum(s =>
            (s.GetProperty("end").GetDateTimeOffset() - s.GetProperty("start").GetDateTimeOffset()).TotalMinutes));
        Assert.Equal(shortfall, plan.GetProperty("warnings").EnumerateArray().Sum(w => w.GetProperty("unscheduledMinutes").GetInt32()));
        if (coverageId != null)
            Assert.Contains(proposed, s => s.GetProperty("preserved").GetBoolean() && s.GetProperty("id").GetGuid() == coverageId);
        var proposalId = plan.GetProperty("id").GetGuid();
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/plans/{proposalId}/apply",
            expected: limited ? HttpStatusCode.Conflict : HttpStatusCode.OK);
        await using var verify = factory.Services.CreateAsyncScope();
        var saved = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(deadline, (await saved.WorkTasks.SingleAsync(ct)).Deadline);
        Assert.Equal(3600, (await saved.SessionExecutions.SingleAsync(ct)).ActualSeconds);
        if (limited) Assert.Equal(1, await saved.WorkSessions.CountAsync(ct));
    }

    [Fact] public async Task End_of_session_workflow_requires_confirmation_then_releases_future_work_on_completion() {
        var ct=TestContext.Current.CancellationToken;await using var f=new ApiFactory();using var client=await f.ClientAsync();
        var tokens=await AuthFlowTests.RegisterLoginAsync(f,client);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens.Access);
        WorkTask task;WorkSession source;
        await using(var scope=f.Services.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var uid=await db.Users.Select(u=>u.Id).SingleAsync(ct);var now=f.Clock.GetUtcNow();
            db.WorkPreferences.Add(new(){UserId=uid,TimeZoneId="UTC",BreakMinutes=0,MinimumSessionMinutes=25,MaximumSessionMinutes=120,MaximumDailyMinutes=1440});
            db.WeeklyAvailability.AddRange(Enumerable.Range(0,7).Select(d=>new WeeklyAvailability{UserId=uid,DayOfWeek=d,StartMinute=0,EndMinute=1440}));
            task=new(){UserId=uid,Name="API report",EstimatedMinutes=120,Deadline=now.AddDays(1),IsHardDeadline=true};
            source=new(){UserId=uid,TaskId=task.Id,Start=now.AddMinutes(-121),End=now.AddMinutes(-1),Status=WorkSessionStatus.NeedsReview};
            db.WorkTasks.Add(task);db.WorkSessions.Add(source);await db.SaveChangesAsync(ct);
        }
        var reviews=await client.GetAsync("/api/sessions/time-review",ct);reviews.EnsureSuccessStatusCode();Assert.Contains(source.Id.ToString(),await reviews.Content.ReadAsStringAsync(ct));
        var response=await client.PostAsJsonAsync($"/api/sessions/{source.Id}/extra-time/preview",new ExtraTimeInput(30),ct);response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var preview=doc.RootElement.GetProperty("data");Assert.True(preview.GetProperty("feasible").GetBoolean());
        var id=preview.GetProperty("id").GetGuid();var revision=preview.GetProperty("revision").GetGuid();
        await using(var scope=f.Services.CreateAsyncScope())Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WorkSessions.CountAsync(ct));
        var endpoint=$"/api/sessions/extra-time/{id}/confirm";
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(endpoint,new ExtraTimeConfirm(revision),ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(endpoint,new ExtraTimeConfirm(revision),ct)).StatusCode);
        await using(var scope=f.Services.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();Assert.Equal(2,await db.WorkSessions.CountAsync(ct));
            var unchanged=await db.WorkTasks.SingleAsync(ct);Assert.Equal(120,unchanged.EstimatedMinutes);Assert.Equal(task.Deadline,unchanged.Deadline);Assert.NotEqual(WorkTaskStatus.Completed,unchanged.Status);
        }
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/sessions/{source.Id}/finish-task",null,ct)).StatusCode);
        await using(var scope=f.Services.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();Assert.Equal(WorkTaskStatus.Completed,(await db.WorkTasks.SingleAsync(ct)).Status);
            Assert.Equal(0,await db.WorkSessions.CountAsync(s=>s.Status==WorkSessionStatus.Scheduled,ct));
        }
        client.DefaultRequestHeaders.Authorization=null;Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/sessions/time-review",ct)).StatusCode);
    }
}
