using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class TeamBriefSqlTests
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    private sealed class Factory : ApiFactory
    {
        public Factory() : base(true) { }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(s => s.AddSingleton<ITaskInterpreter>(new AgentAiTestDouble { BriefResponse = context => new("Dự án", "", "Một gói việc mỗi người",
                Enumerable.Range(1, context.MemberCount).Select(i => new TeamBriefTask("Task " + i, "Nội dung công việc", ["Yêu cầu"], ["Báo cáo"], ["Đầy đủ"], [], ["Đề bài"], 60, context.Now.AddDays(2), "hạn sau hai ngày")).ToArray(), []) }));
        }
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Concurrent_batch_confirmation_creates_whole_batch_once_and_keeps_personal_calendars_untouched()
    {
        var ct = TestContext.Current.CancellationToken; await using var factory = new Factory(); using var client = await factory.ClientAsync();
        var leader = Guid.NewGuid(); var member = Guid.NewGuid(); var teamId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var uid in new[] { leader, member })
            {
                db.Users.Add(new() { Id = uid, Email = uid + "@example.com", NormalizedEmail = (uid + "@example.com").ToUpperInvariant(), UserName = uid.ToString(), FirstName = "Test", LastName = "Member", IsActive = true, EmailConfirmed = true });
                db.WorkPreferences.Add(new() { UserId = uid });
            }
            db.TeamProjects.Add(new() { Id = teamId, Name = "Brief SQL fixture", CreatorId = leader });
            db.TeamMemberships.AddRange(new SPWPOS.Domain.Entities.TeamMembership { TeamId = teamId, UserId = leader, Role = "Leader" }, new SPWPOS.Domain.Entities.TeamMembership { TeamId = teamId, UserId = member });
            await db.SaveChangesAsync(ct);
        }
        TeamBriefDraft draft;
        await using (var scope = factory.Services.CreateAsyncScope())
            draft = await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().DraftTeamBriefAsync(leader, teamId, new("Xây dự án hạn sau hai ngày"), ct);
        Assert.Equal(2, draft.Tasks.Length);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Confirm()
        {
            await using var scope = factory.Services.CreateAsyncScope(); await gate.Task;
            try { await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().ConfirmTeamBriefAsync(leader, teamId, draft.ProposalId, ct); }
            catch (DbUpdateConcurrencyException) { }
            catch (AuthException e) when (e.Code == "WORKSPACE_STALE") { }
        }
        var first = Confirm(); var second = Confirm(); gate.SetResult(); await Task.WhenAll(first, second);
        await using var verify = factory.Services.CreateAsyncScope(); var final = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await final.TeamTasks.CountAsync(ct)); Assert.True((await final.PrivatePlanningProposals.SingleAsync(ct)).Applied);
        Assert.Equal(2, await final.TeamActivities.CountAsync(x => x.Kind == "TASK_CREATED", ct));
        Assert.Equal(0, await final.WorkTasks.CountAsync(ct)); Assert.Equal(0, await final.WorkSessions.CountAsync(ct));
        var replay = await verify.ServiceProvider.GetRequiredService<IPrivateTeamService>().ConfirmTeamBriefAsync(leader, teamId, draft.ProposalId, ct);
        Assert.Equal(2, replay.TaskIds.Length); Assert.Equal(2, await final.TeamTasks.CountAsync(ct));
    }
}
