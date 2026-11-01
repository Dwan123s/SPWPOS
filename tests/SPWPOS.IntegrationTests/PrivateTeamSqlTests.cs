using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class PrivateTeamSqlTests
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Simultaneous_claims_have_one_winner_and_no_private_writes_for_loser()
    {
        var ct = TestContext.Current.CancellationToken; await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync();
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var teamId = Guid.NewGuid(); var taskId = Guid.NewGuid(); var now = factory.Clock.GetUtcNow();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var uid in new[] { a, b })
            {
                db.Users.Add(new() { Id = uid, Email = uid + "@example.com", NormalizedEmail = (uid + "@example.com").ToUpperInvariant(), UserName = uid.ToString(), FirstName = "Test", LastName = "Member", IsActive = true, EmailConfirmed = true });
                db.WorkPreferences.Add(new() { UserId = uid, TimeZoneId = "UTC", BreakMinutes = 0 });
                db.FixedEvents.Add(new() { UserId = uid, Name = "PRIVATE", Start = now.AddHours(3), End = now.AddHours(4) });
                foreach (var day in Enumerable.Range(0, 7)) db.WeeklyAvailability.Add(new() { UserId = uid, DayOfWeek = day, StartMinute = 0, EndMinute = 1440 });
            }
            db.TeamProjects.Add(new() { Id = teamId, Name = "Disposable race fixture", CreatorId = a }); db.TeamMemberships.AddRange(new TeamMembership { TeamId = teamId, UserId = a, Role = "Leader" }, new TeamMembership { TeamId = teamId, UserId = b });
            db.TeamTasks.Add(new() { Id = taskId, TeamId = teamId, Name = "Shared", EstimatedMinutes = 60, Deadline = now.AddDays(2) }); await db.SaveChangesAsync(ct);
        }
        async Task<PrivateClaimView> Preview(Guid uid) { await using var scope = factory.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().PreviewTeamClaimAsync(uid, taskId, ct); }
        var pa = await Preview(a); var pb = await Preview(b); Assert.NotEmpty(pa.Options); Assert.NotEmpty(pb.Options);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Claim(Guid uid, Guid id)
        {
            await using var scope = factory.Services.CreateAsyncScope(); await gate.Task;
            try { await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().ConfirmTeamClaimAsync(uid, id, new(), ct); return true; }
            catch (Exception e) when (e is DbUpdateConcurrencyException || e is AuthException { Code: "CLAIM_STALE" or "TASK_UNAVAILABLE" }) { return false; }
        }
        var ca = Claim(a, pa.Id); var cb = Claim(b, pb.Id); gate.SetResult(); var results = await Task.WhenAll(ca, cb); Assert.Single(results, x => x);
        await using var verify = factory.Services.CreateAsyncScope(); var final = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assignment = await final.TeamTaskAssignments.SingleAsync(ct); Assert.True(assignment.Active); Assert.Equal(1, await final.WorkTasks.CountAsync(ct));
        Assert.True(await final.WorkSessions.AnyAsync(x => x.UserId == assignment.UserId, ct)); Assert.False(await final.WorkSessions.AnyAsync(x => x.UserId != assignment.UserId, ct));
        Assert.Equal(assignment.UserId, (await final.TeamTasks.SingleAsync(ct)).AssigneeId);
        var service = verify.ServiceProvider.GetRequiredService<IPrivateTeamService>(); var chat = new TeamChatInput(Guid.NewGuid(), "Tin thử nghiệm SQL");
        await service.SendTeamChatAsync(a, teamId, chat, ct); await service.SendTeamChatAsync(a, teamId, chat, ct);
        Assert.Equal(chat.Text, Assert.Single((await service.GetTeamChatAsync(b, teamId, null, null, ct)).Items).Text);
        // Concurrent reviewers must produce exactly one completion announcement.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await database.TeamMemberships.SingleAsync(x => x.UserId == b, ct)).Role = "Leader";
            await database.SaveChangesAsync(ct);
        }
        SharedTaskView pending;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var teams = scope.ServiceProvider.GetRequiredService<IPrivateTeamService>();
            var assigned = (await teams.GetTeamAsync(assignment.UserId, teamId, ct)).Tasks.Single();
            await teams.UpdateTeamProgressAsync(assignment.UserId, taskId, new(assigned.Version, 100), ct);
            pending = (await teams.GetTeamAsync(a, teamId, ct)).Tasks.Single();
        }
        Assert.Equal("PendingReview", pending.Status);
        var reviewGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var review = new TeamCompletionReviewInput(pending.Version, pending.CompletionRequestId!.Value, true);
        async Task Approve(Guid reviewer)
        {
            await using var scope = factory.Services.CreateAsyncScope(); await reviewGate.Task;
            try { await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().ReviewTeamCompletionAsync(reviewer, taskId, review, ct); }
            catch (Exception e) when (e is DbUpdateConcurrencyException || e is AuthException { Code: "TASK_STALE" }) { }
        }
        var ra = Approve(a); var rb = Approve(b); reviewGate.SetResult(); await Task.WhenAll(ra, rb);
        await using var checkScope = factory.Services.CreateAsyncScope();
        var check = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Completed", (await check.TeamTasks.AsNoTracking().SingleAsync(ct)).Status);
        Assert.Equal(1, await check.TeamChatMessages.CountAsync(x => x.IsSystem, ct));
        Assert.Equal(2, await check.InAppNotifications.CountAsync(x => x.Kind == "TEAM_TASK_COMPLETED", ct));
        Assert.Equal(1, await check.TeamActivities.CountAsync(x => x.Kind == "TASK_COMPLETION_APPROVED", ct));
        var reviewerService = checkScope.ServiceProvider.GetRequiredService<IPrivateTeamService>();
        await reviewerService.ReviewTeamCompletionAsync(a, taskId, review, ct);
        Assert.Equal(1, await check.TeamChatMessages.CountAsync(x => x.IsSystem, ct));
    }
}
