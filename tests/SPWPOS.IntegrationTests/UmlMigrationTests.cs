using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class UmlMigrationTests
{
    public static bool SqlServerAvailable => SqlServerConcurrencyTests.SqlServerAvailable;
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Conversation_upgrade_backfills_each_owner_and_keeps_turns_unchanged()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var factory=new ApiFactory(true);using var client=await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory,client);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrations=(await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();var index=Array.FindIndex(migrations,x=>x.EndsWith("_AddAgentConversationLifecycle",StringComparison.Ordinal));Assert.True(index>0);
        var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync(migrations[index-1],ct);
        var uid=await db.Users.Select(x=>x.Id).SingleAsync(ct);var conversation=Guid.NewGuid();var now=factory.Clock.GetUtcNow();
        foreach(var offset in new[]{-1,0})
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO AgentTurns(Id,UserId,ConversationId,RequestId,Text,Status,CommandJson,ChangesJson,ResultJson,UndoJson,ExpectedRevision,CreatedAt,UpdatedAt,ExpiresAt)
                VALUES({Guid.NewGuid()},{uid},{conversation},{Guid.NewGuid()},{(offset==-1?"Legacy conversation":"Follow-up")},'ANSWERED','','','','',{Guid.Empty},{now.AddMinutes(offset)},{now.AddMinutes(offset)},{now.AddMinutes(30)});
                """,ct);
        }
        await migrator.MigrateAsync(null,ct);
        var row=await db.AgentConversations.SingleAsync(ct);Assert.Equal(uid,row.UserId);Assert.Equal(conversation,row.Id);Assert.Equal("Legacy conversation",row.Title);
        Assert.Equal(now.AddMinutes(-1),row.CreatedAt);Assert.Equal(now,row.UpdatedAt);Assert.Equal(2,await db.AgentTurns.CountAsync(ct));
        Assert.Equal("Follow-up",await db.AgentTurns.OrderByDescending(x=>x.CreatedAt).Select(x=>x.Text).FirstAsync(ct));
    }
    [Fact(SkipUnless = nameof(SqlServerAvailable), Skip = "Requires SPWPOS_TEST_SQLSERVER."), Trait("Category", "SqlServer")]
    public async Task Upgrade_preserves_legacy_data_and_backfills_plan_execution_profile_and_observations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(true); using var client = await factory.ClientAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // ApiFactory owns a new SPWPOS_Tests_<random> database and deletes only that database.
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261004104534_AddWorkloadSystem", ct);
        var uid = Guid.NewGuid(); var tid = Guid.NewGuid(); var sid = Guid.NewGuid(); var pid = Guid.NewGuid(); var now = factory.Clock.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO AspNetUsers(Id,FirstName,LastName,CreatedAt,UpdatedAt,IsActive,Email,NormalizedEmail,EmailConfirmed,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount)
            VALUES ({uid},'Legacy','User',{now},{now},1,'legacy@example.com','LEGACY@EXAMPLE.COM',1,0,0,1,0);
            INSERT INTO WorkTasks(Id,UserId,Name,Description,Deadline,Priority,Status,Progress,EstimatedMinutes,TaskType,Splittable,MinimumSessionMinutes,IsDeleted,CreatedAt,UpdatedAt,CompletedAt)
            VALUES({tid},{uid},'Legacy task','',{now},2,2,100,60,'Programming',1,25,1,{now},{now},{now});
            INSERT INTO WorkSessions(Id,UserId,TaskId,Start,[End],Status,IsLocked,ActualSeconds,ReasonCode,CompletedAt)
            VALUES({sid},{uid},{tid},{now.AddHours(-2)},{now.AddHours(-1)},3,0,5400,'EARLIEST_FEASIBLE_SLOT',{now});
            """, ct);
        var payload = JsonSerializer.Serialize(new PlanView(pid, Guid.Empty, now.AddHours(-2), now,
            [new(sid, tid, now.AddHours(-2), now.AddHours(-1), "EARLIEST_FEASIBLE_SLOT", false)], [], [], true));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO PlanProposals(Id,UserId,CreatedAt,ExpiresAt,Revision,PayloadJson,IsApplied)
            VALUES({pid},{uid},{now.AddHours(-3)},{now}, {Guid.Empty},{payload},1);
            """, ct);
        await migrator.MigrateAsync(null, ct);
        var task = await db.WorkTasks.SingleAsync(ct); Assert.Equal(tid, task.Id); Assert.True(task.IsDeleted); Assert.True(task.IsHardDeadline); Assert.NotNull(task.DeletedAt);
        Assert.Equal("PROGRAMMING", task.TaskType); Assert.Equal(60, task.EstimatedMinutes);
        Assert.Equal("Legacy User", (await db.UserProfiles.SingleAsync(ct)).DisplayName);
        Assert.Equal("APPLIED", (await db.PlanProposals.SingleAsync(ct)).Status); Assert.Single(await db.PlanItems.ToArrayAsync(ct));
        Assert.NotNull((await db.WorkSessions.SingleAsync(ct)).SourceItemId); Assert.Equal(5400, (await db.SessionExecutions.SingleAsync(ct)).TimerSeconds);
        Assert.Equal(90, (await db.EstimationObservations.SingleAsync(ct)).ActualMinutes);
        Assert.Single(await db.NotificationSettings.ToArrayAsync(ct));
    }
}
