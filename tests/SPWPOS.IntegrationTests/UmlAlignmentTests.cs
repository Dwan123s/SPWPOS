using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class UmlAlignmentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact] public async Task Long_running_timer_is_preserved_but_expired_resume_requires_replanning()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        await Create(client, factory.Clock.GetUtcNow());
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 0, 0, 1440));
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(factory.Clock.GetUtcNow(), 1))).GetProperty("data");
        await Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply");
        var sid = plan.GetProperty("sessions")[0].GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/start"); factory.Clock.Advance(TimeSpan.FromHours(25));
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/pause"); factory.Clock.Advance(TimeSpan.FromHours(3));
        var resumed = await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/resume", expected: HttpStatusCode.Conflict);
        Assert.Equal("SESSION_WINDOW_EXPIRED", resumed.GetProperty("error").GetProperty("code").GetString());
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/complete");
        var history = (await Send(client, HttpMethod.Get, $"/api/sessions/{sid}/execution")).GetProperty("data");
        Assert.Equal(25*3600, history.GetProperty("timerSeconds").GetInt64()); Assert.Equal(25*3600, history.GetProperty("actualSeconds").GetInt64());
    }
    [Fact] public async Task Plan_decisions_remain_proposals_until_apply_then_supersede_previous_plan()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        await Create(client, factory.Clock.GetUtcNow());
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(factory.Clock.GetUtcNow(), 1))).GetProperty("data"); var id = plan.GetProperty("id").GetGuid();
        var decisions = (await Send(client, HttpMethod.Get, $"/api/plans/{id}/items")).GetProperty("data"); Assert.NotEmpty(decisions.EnumerateArray());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); Assert.Empty(await db.WorkSessions.ToArrayAsync(Ct));
            Assert.NotEmpty(await db.PlanItems.ToArrayAsync(Ct)); Assert.NotEmpty(await db.PlanItemReasons.ToArrayAsync(Ct));
        }
        await Send(client, HttpMethod.Post, $"/api/plans/{id}/apply");
        var next = (await Send(client, HttpMethod.Post, "/api/plans/reschedule", new PlanInput(factory.Clock.GetUtcNow(), 1))).GetProperty("data"); var nextId = next.GetProperty("id").GetGuid();
        Assert.Equal("RESCHEDULE", next.GetProperty("kind").GetString()); Assert.Equal(id, next.GetProperty("previousPlanId").GetGuid());
        await Send(client, HttpMethod.Post, $"/api/plans/{nextId}/apply");
        Assert.Equal("SUPERSEDED", (await Send(client, HttpMethod.Get, $"/api/plans/{id}")).GetProperty("data").GetProperty("status").GetString());
        await using var finalScope = factory.Services.CreateAsyncScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await finalDb.WorkSessions.Where(x => x.Status == SPWPOS.Domain.Entities.WorkSessionStatus.Scheduled).ToArrayAsync(Ct), x => Assert.NotNull(x.SourceItemId));
    }
    [Fact] public async Task Discarded_proposal_is_not_applicable_and_failure_is_logged_after_rollback()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        await Create(client, factory.Clock.GetUtcNow());
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(factory.Clock.GetUtcNow(), 1))).GetProperty("data"); var id = plan.GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Post, $"/api/plans/{id}/discard");
        await Send(client, HttpMethod.Post, $"/api/plans/{id}/apply", expected: HttpStatusCode.Conflict);
        await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(factory.Clock.GetUtcNow().AddYears(5)), HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.WorkSessions.ToArrayAsync(Ct));
        Assert.Contains(await db.SystemJobRuns.ToArrayAsync(Ct), x => x.Status == "FAILED" && x.ErrorMessage == "INVALID_HORIZON");
    }
    [Fact] public async Task Timer_segments_and_original_estimate_survive_adjustment_and_soft_delete()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client); await SetupTime(client);
        var now = factory.Clock.GetUtcNow(); var taskId = await Create(client, now);
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 0, 0, 1440));
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(now, 1))).GetProperty("data");
        await Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply");
        var sid = plan.GetProperty("sessions")[0].GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/start"); factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/pause"); factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/resume"); factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/complete");
        factory.Clock.Advance(TimeSpan.FromHours(1)); // Capacity samples only include sessions whose planned end has elapsed.
        await Send(client, HttpMethod.Patch, $"/api/tasks/{taskId}/progress", new ProgressInput(100));
        await Send(client, HttpMethod.Put, $"/api/sessions/{sid}/actual-duration", new DurationInput(45));
        var history = (await Send(client, HttpMethod.Get, $"/api/sessions/{sid}/execution")).GetProperty("data");
        Assert.Equal(1800, history.GetProperty("timerSeconds").GetInt64()); Assert.Equal(2700, history.GetProperty("actualSeconds").GetInt64());
        Assert.True(history.GetProperty("isUserAdjusted").GetBoolean()); Assert.Equal(2, history.GetProperty("segments").GetArrayLength());
        await Send(client, HttpMethod.Delete, $"/api/tasks/{taskId}");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var observation = Assert.Single(await db.EstimationObservations.ToArrayAsync(Ct)); Assert.Equal(60, observation.EstimatedMinutes); Assert.Equal(45, observation.ActualMinutes);
        Assert.NotNull((await db.WorkTasks.SingleAsync(Ct)).DeletedAt); Assert.NotEmpty(await db.PersonalCapacityProfiles.ToArrayAsync(Ct));
        Assert.NotEmpty(await db.CapacitySnapshots.ToArrayAsync(Ct));
    }
    [Fact] public async Task Subtask_deadline_must_fit_parent_and_profile_event_preferences_are_saved()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        var now = factory.Clock.GetUtcNow(); var parentId = await Create(client, now);
        await Send(client, HttpMethod.Post, "/api/tasks", new TaskInput("Invalid child", "", now.AddDays(2), 60, ParentId: parentId), HttpStatusCode.BadRequest);
        await Send(client, HttpMethod.Post, "/api/tasks", new TaskInput("Child", "", now.AddHours(10), 60, ParentId: parentId));
        await Send(client, HttpMethod.Put, $"/api/tasks/{parentId}", new TaskInput("Parent", "", now.AddHours(1), 60), HttpStatusCode.BadRequest);
        await Send(client, HttpMethod.Put, "/api/workspace/profile", new ProfileInput("Minh", "Nguyen", "Minh Workspace"));
        await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Class", now.AddDays(1), now.AddDays(1).AddHours(1), "Review", "CLASS"));
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 10, 0, 1440, 30, "MORNING"));
        var state = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal("Minh Workspace", state.GetProperty("profile").GetProperty("displayName").GetString());
        Assert.Equal("CLASS", state.GetProperty("events")[0].GetProperty("eventType").GetString());
        Assert.Equal(30, state.GetProperty("preference").GetProperty("planningHorizonDays").GetInt32());
    }
    [Fact] public async Task Notification_opt_out_and_failed_ai_job_are_persistent_without_saving_suggestions()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        await Send(client, HttpMethod.Put, "/api/notifications/settings", new NotificationSettingInput(false, 12, 5, "OVERLOAD"));
        await Create(client, factory.Clock.GetUtcNow());
        var inbox = (await Send(client, HttpMethod.Get, "/api/notifications")).GetProperty("data"); Assert.Empty(inbox.EnumerateArray());
        var suggestion = (await Send(client, HttpMethod.Post, "/api/tasks/capture", new CaptureInput("Viết báo cáo ngày mai", true))).GetProperty("data");
        Assert.Equal("NATURAL_LANGUAGE", suggestion.GetProperty("task").GetProperty("creationSource").GetString());
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.WorkTasks.ToArrayAsync(Ct)); Assert.Contains(await db.SystemJobRuns.ToArrayAsync(Ct), x => x.JobType == "AI_REQUEST" && x.Status == "FAILED");
        Assert.Equal("OVERLOAD", (await db.NotificationSettings.SingleAsync(Ct)).WorkloadWarningLevel);
    }
    [Fact] public async Task What_if_and_ad_hoc_conflict_checks_do_not_persist_history()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync(); await Authorize(factory, client);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plans = await db.PlanProposals.CountAsync(Ct); var risks = await db.RiskAssessments.CountAsync(Ct); var jobs = await db.SystemJobRuns.CountAsync(Ct);
        var now = factory.Clock.GetUtcNow();
        await Send(client, HttpMethod.Post, "/api/plans/scenario", new ScenarioInput(new(now, 7), new("Simulated", "", now.AddDays(1), 60)));
        await Send(client, HttpMethod.Post, "/api/plans/conflicts", new PlanInput(now, 7));
        Assert.Equal(plans, await db.PlanProposals.CountAsync(Ct)); Assert.Equal(risks, await db.RiskAssessments.CountAsync(Ct)); Assert.Equal(jobs, await db.SystemJobRuns.CountAsync(Ct));
        Assert.Empty(await db.WorkTasks.ToArrayAsync(Ct));
    }
}
