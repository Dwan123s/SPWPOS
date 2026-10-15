using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class ConversationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h, Func<IPlanningAgentService,Task<T>> action)
    {await using var scope=h.Provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<IPlanningAgentService>());}
    [Fact] public async Task Server_search_pagination_rename_archive_delete_and_replay_preserve_ownership()
    {
        var ai=new AgentAiTestDouble();await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();
        var conversation=Guid.NewGuid();var input=new AgentTurnInput(conversation,Guid.NewGuid(),"Hỏi lịch hôm nay",false);
        var turn=await Work(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));
        await h.DbAsync(async db=>{
            var existing=await db.AgentConversations.SingleAsync(Ct); existing.UpdatedAt=h.Clock.GetUtcNow().AddHours(-1);
            db.AgentTurns.AddRange(Enumerable.Range(0,120).Select(i=>new AgentTurn {UserId=user.Id,ConversationId=conversation,RequestId=Guid.NewGuid(),Text=$"Old {i}",Status="ANSWERED",CreatedAt=h.Clock.GetUtcNow().AddMinutes(-i-1),UpdatedAt=h.Clock.GetUtcNow()}));
            db.AgentConversations.AddRange(Enumerable.Range(0,35).Select(i=>new AgentConversation {UserId=user.Id,Title=$"Extra {i}",CreatedAt=h.Clock.GetUtcNow(),UpdatedAt=h.Clock.GetUtcNow().AddMinutes(i)}));await db.SaveChangesAsync(Ct);});
        var first=await Work(h,s=>s.GetConversationsAsync(user.Id,null,false,null,30,Ct));Assert.Equal(30,first.Items.Length);Assert.NotNull(first.NextCursor);
        var second=await Work(h,s=>s.GetConversationsAsync(user.Id,null,false,first.NextCursor,30,Ct));Assert.Equal(6,second.Items.Length);Assert.Empty(first.Items.Select(x=>x.Id).Intersect(second.Items.Select(x=>x.Id)));
        var search=await Work(h,s=>s.GetConversationsAsync(user.Id,"Old 119",false,null,30,Ct));var row=Assert.Single(search.Items);Assert.Equal(conversation,row.Id);
        var page=await Work(h,s=>s.GetConversationTurnsAsync(user.Id,conversation,null,100,Ct));Assert.Equal(100,page.Items.Length);
        var older=await Work(h,s=>s.GetConversationTurnsAsync(user.Id,conversation,page.NextCursor,100,Ct));Assert.Equal(21,older.Items.Length);
        var changed=await Work(h,s=>s.EditConversationAsync(user.Id,conversation,new("Lịch làm việc",true,row.Version),Ct));
        Assert.Empty((await Work(h,s=>s.GetConversationsAsync(user.Id,"Lịch làm việc",false,null,30,Ct))).Items);
        Assert.Single((await Work(h,s=>s.GetConversationsAsync(user.Id,"Lịch làm việc",true,null,30,Ct))).Items);
        Assert.Equal(121,(await Work(h,s=>s.ExportConversationAsync(user.Id,conversation,Ct))).Turns.Length);
        Assert.Equal("NOT_FOUND",(await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.GetConversationTurnsAsync(Guid.NewGuid(),conversation,null,50,Ct)))).Code);
        await Work(h,async s=>{await s.DeleteConversationAsync(user.Id,conversation,changed.Version,Ct);return true;});
        var replay=await Work(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));Assert.Equal("DELETED",replay.Status);Assert.Equal(1,ai.Calls);Assert.Empty(replay.Text);
        await h.DbAsync(async db=>{Assert.False(await db.AgentTurns.AnyAsync(x=>x.Text!="" || x.UndoJson!="" || x.ResultJson!="",Ct));});
        Assert.Equal("NOT_FOUND",(await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.ExportConversationAsync(user.Id,conversation,Ct)))).Code);
        Assert.Equal("AGENT_REQUEST_REUSED",(await Assert.ThrowsAsync<AuthException>(()=>Work(h,s=>s.SendAgentTurnAsync(user.Id,input with {Text="Khác"},Ct)))).Code);
    }
    [Fact] public async Task History_no_longer_has_a_permanent_two_thousand_turn_limit()
    {
        var ai=new AgentAiTestDouble();await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();
        await h.DbAsync(async db=>{db.AgentTurns.AddRange(Enumerable.Range(0,2000).Select(i=>new AgentTurn {UserId=user.Id,ConversationId=Guid.NewGuid(),RequestId=Guid.NewGuid(),Text="Old",Status="ANSWERED",CreatedAt=h.Clock.GetUtcNow(),UpdatedAt=h.Clock.GetUtcNow()}));await db.SaveChangesAsync(Ct);});
        var result=await Work(h,s=>s.SendAgentTurnAsync(user.Id,new(Guid.NewGuid(),Guid.NewGuid(),"Xem lịch",false),Ct));Assert.Equal("ANSWERED",result.Status);Assert.Equal(1,ai.Calls);
    }
}
