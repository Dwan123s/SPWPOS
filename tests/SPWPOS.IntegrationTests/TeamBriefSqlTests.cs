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
            draft = await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().DraftTeamBriefAsync(leader, teamId,
                new("Xây dự án hạn sau hai ngày", new("Đề bài.txt", "text/plain", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Yêu cầu gốc của dự án")))), ct);
        Assert.Equal(2, draft.Tasks.Length);
        var recipients = new TeamBriefConfirmInput([new(draft.Tasks[0].Id, member), new(draft.Tasks[1].Id, leader)]);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Confirm()
        {
            await using var scope = factory.Services.CreateAsyncScope(); await gate.Task;
            try { await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().ConfirmTeamBriefAsync(leader, teamId, draft.ProposalId, ct, recipients); }
            catch (DbUpdateConcurrencyException) { }
            catch (AuthException e) when (e.Code == "WORKSPACE_STALE") { }
        }
        var first = Confirm(); var second = Confirm(); gate.SetResult(); await Task.WhenAll(first, second);
        await using var verify = factory.Services.CreateAsyncScope(); var final = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await final.TeamTasks.CountAsync(ct)); Assert.True((await final.PrivatePlanningProposals.SingleAsync(ct)).Applied);
        Assert.Equal(2, await final.TeamActivities.CountAsync(x => x.Kind == "TASK_CREATED", ct));
        Assert.Equal(0, await final.WorkTasks.CountAsync(ct)); Assert.Equal(0, await final.WorkSessions.CountAsync(ct));
        var replay = await verify.ServiceProvider.GetRequiredService<IPrivateTeamService>().ConfirmTeamBriefAsync(leader, teamId, draft.ProposalId, ct, recipients);
        Assert.Equal(2, replay.TaskIds.Length); Assert.Equal(2, await final.TeamTasks.CountAsync(ct));
        Assert.Equal(1, await final.TeamTaskDocuments.CountAsync(ct));
        Assert.Equal(2, await final.TeamTasks.CountAsync(x => x.SourceDocumentId == draft.ProposalId, ct));
        var downloaded = await verify.ServiceProvider.GetRequiredService<IPrivateTeamService>().DownloadTeamSourceFileAsync(member, replay.TaskIds[0], draft.ProposalId, ct);
        Assert.Equal("Yêu cầu gốc của dự án", System.Text.Encoding.UTF8.GetString(downloaded.Content));
        Assert.Equal(member, (await final.TeamTasks.SingleAsync(t => t.Id == draft.Tasks[0].Id, ct)).DesignatedUserId);
        Assert.Equal(2, await final.InAppNotifications.CountAsync(n => n.Kind == "TEAM_TASK_DESIGNATED", ct));
    }
}
