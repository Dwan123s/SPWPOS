using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamCompletionFileSqlTests
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Simultaneous_report_submissions_store_one_binary_file_and_one_review_request()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new ApiFactory(true); using var client = await f.ClientAsync();
        var leader = Guid.NewGuid(); var member = Guid.NewGuid(); var team = Guid.NewGuid(); var taskId = Guid.NewGuid(); var version = Guid.NewGuid();
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var uid in new[] { leader, member })
            {
                db.Users.Add(new() { Id = uid, UserName = uid.ToString(), Email = uid + "@example.com", NormalizedEmail = (uid + "@example.com").ToUpperInvariant(), FirstName = "Test", LastName = "Report", IsActive = true, EmailConfirmed = true });
                db.WorkPreferences.Add(new() { UserId = uid });
            }
            db.TeamProjects.Add(new() { Id = team, CreatorId = leader, Name = "SQL report fixture" });
            db.TeamMemberships.AddRange(new TeamMembership { TeamId = team, UserId = leader, Role = "Leader" }, new TeamMembership { TeamId = team, UserId = member });
            var personal = new WorkTask { UserId = member, Name = "Report", Deadline = f.Clock.GetUtcNow().AddDays(2), EstimatedMinutes = 60 }; db.WorkTasks.Add(personal);
            db.TeamTasks.Add(new() { Id = taskId, TeamId = team, AssigneeId = member, Status = "Assigned", Version = version, Name = "Report", Deadline = personal.Deadline, EstimatedMinutes = 60 });
            db.TeamTaskAssignments.Add(new() { TeamTaskId = taskId, UserId = member, WorkTaskId = personal.Id, ProposalId = Guid.NewGuid() }); await db.SaveChangesAsync(ct);
        }
        var input = new TeamCompletionSubmitInput(version, Guid.NewGuid(), "Kết quả riêng", [new("report.txt", Convert.ToBase64String(Encoding.UTF8.GetBytes("SQL report bytes")))]);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Submit()
        {
            await using var scope = f.Services.CreateAsyncScope(); await gate.Task;
            try { await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().SubmitTeamCompletionAsync(member, taskId, input, ct); }
            catch (DbUpdateConcurrencyException) { }
        }
        var a = Submit(); var b = Submit(); gate.SetResult(); await Task.WhenAll(a, b);
        await using var verify = f.Services.CreateAsyncScope(); var dbFinal = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbFinal.TeamCompletionSubmissions.CountAsync(ct)); var file = await dbFinal.TeamCompletionFiles.SingleAsync(ct);
        Assert.Equal("PendingReview", (await dbFinal.TeamTasks.SingleAsync(ct)).Status);
        Assert.Equal(1, await dbFinal.InAppNotifications.CountAsync(x => x.Kind == "TEAM_COMPLETION_REQUEST", ct));
        var service = verify.ServiceProvider.GetRequiredService<IPrivateTeamService>();
        var download = await service.DownloadTeamCompletionFileAsync(leader, taskId, file.Id, ct);
        Assert.Equal("SQL report bytes", Encoding.UTF8.GetString(download.Content));
        await service.SubmitTeamCompletionAsync(member, taskId, input, ct); Assert.Equal(1, await dbFinal.TeamCompletionFiles.CountAsync(ct));
    }
}
