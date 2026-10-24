using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class UrgentPlanApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Scoped_new_task_plans_protect_existing_unscheduled_deadlines_and_reject_infeasible_apply(bool insufficient)
    {
        await using var f = new ApiFactory();
        var day = new DateTimeOffset(f.Clock.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 120, 0, 480, 1200));
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/availability", new WeeklyInput([new((int)day.DayOfWeek, 480, insufficient ? 540 : 600)]));
        var oldId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/tasks",
            new TaskInput("Việc gần hạn", "", day.AddHours(1), 60, TaskType: "OTHER", Splittable: false, IsHardDeadline: true))).GetProperty("data").GetProperty("id").GetGuid();
        var addedId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/tasks",
            new TaskInput("Việc mới", "", day.AddHours(2), 60, TaskType: "OTHER", Splittable: false, IsHardDeadline: true))).GetProperty("data").GetProperty("id").GetGuid();
        var plan = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans",
            new PlanInput(day, 1, [addedId], true, UseCapacityFactor: false, UseEstimateBias: false))).GetProperty("data");
        Assert.True(plan.GetProperty("hasDeadlineProtection").GetBoolean());
        Assert.Equal(day, Assert.Single(plan.GetProperty("sessions").EnumerateArray(), x => x.GetProperty("taskId").GetGuid() == oldId).GetProperty("start").GetDateTimeOffset());
        var response = await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply",
            expected: insufficient ? HttpStatusCode.Conflict : HttpStatusCode.OK);
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        if (insufficient) {
            Assert.Equal("DEADLINE_PROTECTION_INFEASIBLE", response.GetProperty("error").GetProperty("code").GetString());
            Assert.Empty(workspace.GetProperty("sessions").EnumerateArray());
        } else {
            var sessions = workspace.GetProperty("sessions").EnumerateArray().OrderBy(x => x.GetProperty("start").GetDateTimeOffset()).ToArray();
            Assert.Equal(2, sessions.Length); Assert.Equal(oldId, sessions[0].GetProperty("taskId").GetGuid());
            Assert.Equal(day.AddHours(1), sessions[1].GetProperty("start").GetDateTimeOffset());
        }
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Manual_urgent_planning_displaces_work_using_the_near_or_distant_deadline_policy(bool nearDeadline)
    {
        await using var f = new ApiFactory();
        var now = f.Clock.GetUtcNow(); var day = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        await WorkspaceFlowTests.SetupTime(client);
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 120, 0, 480, 1200));
        var due = nearDeadline ? day.AddHours(8) : day.AddDays(7);
        var oldId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/tasks",
            new TaskInput("Việc cũ", "", due, 60, TaskType: "OTHER", Splittable: false, IsHardDeadline: true))).GetProperty("data").GetProperty("id").GetGuid();
        var sessionId = Guid.NewGuid();
        await using (var scope = f.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.WorkSessions.Add(new() { Id = sessionId, UserId = await db.Users.Select(x => x.Id).SingleAsync(Ct), TaskId = oldId,
                Start = day.AddHours(2), End = day.AddHours(3) }); await db.SaveChangesAsync(Ct);
        }
        var urgentId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/tasks",
            new TaskInput("Deadline khẩn", "", day.AddHours(3), 60, 4, "OTHER", false, IsHardDeadline: true))).GetProperty("data").GetProperty("id").GetGuid();
        var plan = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans",
            new PlanInput(day.AddHours(2), 7, [urgentId], true, UseCapacityFactor: false, UseEstimateBias: false))).GetProperty("data");
        Assert.True(plan.GetProperty("hasUrgentReplan").GetBoolean()); Assert.Empty(plan.GetProperty("warnings").EnumerateArray());
        var before = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(sessionId, Assert.Single(before.GetProperty("sessions").EnumerateArray()).GetProperty("id").GetGuid());
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply");
        var after = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        var active = after.GetProperty("sessions").EnumerateArray().Where(x => x.GetProperty("status").GetString() == "Scheduled").ToArray();
        Assert.Equal(2, active.Length);
        Assert.Equal(day.AddHours(2), Assert.Single(active, x => x.GetProperty("taskId").GetGuid() == urgentId).GetProperty("start").GetDateTimeOffset());
        Assert.Equal(nearDeadline ? day.AddHours(3) : day.AddDays(1).AddHours(2),
            Assert.Single(active, x => x.GetProperty("taskId").GetGuid() == oldId).GetProperty("start").GetDateTimeOffset());
        Assert.Equal(due, Assert.Single(after.GetProperty("tasks").EnumerateArray(), x => x.GetProperty("id").GetGuid() == oldId).GetProperty("deadline").GetDateTimeOffset());
    }

    [Fact]
    public async Task Applying_an_infeasible_urgent_replan_cannot_cancel_the_old_scheduled_work()
    {
        await using var f = new ApiFactory();
        var now = f.Clock.GetUtcNow(); var day = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        using var client = await f.ClientAsync(); await WorkspaceFlowTests.Authorize(f, client);
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 120, 0, 480, 1200));
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/availability", new WeeklyInput([new((int)day.DayOfWeek, 600, 660)]));
        var oldId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/tasks",
            new TaskInput("Hạn cũ", "", day.AddHours(3), 60, TaskType: "OTHER", IsHardDeadline: true))).GetProperty("data").GetProperty("id").GetGuid();
        var sessionId = Guid.NewGuid();
        await using (var scope = f.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.WorkSessions.Add(new() { Id = sessionId, UserId = await db.Users.Select(x => x.Id).SingleAsync(Ct), TaskId = oldId,
                Start = day.AddHours(2), End = day.AddHours(3) }); await db.SaveChangesAsync(Ct);
        }
        var urgentId = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/tasks",
            new TaskInput("Việc khẩn", "", day.AddHours(3), 60, 4, "OTHER", IsHardDeadline: true))).GetProperty("data").GetProperty("id").GetGuid();
        var plan = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans",
            new PlanInput(day.AddHours(2), 1, [urgentId], true, UseCapacityFactor: false, UseEstimateBias: false))).GetProperty("data");
        Assert.NotEmpty(plan.GetProperty("warnings").EnumerateArray());
        var rejection = await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply", expected: HttpStatusCode.Conflict);
        Assert.Equal("URGENT_REPLAN_INFEASIBLE", rejection.GetProperty("error").GetProperty("code").GetString());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(sessionId, Assert.Single(workspace.GetProperty("sessions").EnumerateArray()).GetProperty("id").GetGuid());
    }
}
