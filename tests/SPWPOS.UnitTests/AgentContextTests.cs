using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class AgentContextTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    [Fact] public async Task Named_task_beyond_the_default_page_is_included_and_truncation_is_disclosed()
    {
        var ai=new AgentAiTestDouble {Response=_=>AgentAiTestDouble.Empty() with {Question="Bạn chọn việc nào?",Options=["Báo cáo A","Báo cáo B"]}};
        await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(ai));var user=await h.RegisterAsync();
        var target=new WorkTask{UserId=user.Id,Name="Báo cáo hiếm",Deadline=h.Clock.GetUtcNow().AddDays(30),CreatedAt=h.Clock.GetUtcNow()};
        await h.DbAsync(async db=>{db.WorkTasks.Add(target);db.WorkTasks.AddRange(Enumerable.Range(0,120).Select(i=>new WorkTask{UserId=user.Id,Name=$"Work {i}",Deadline=h.Clock.GetUtcNow().AddDays(1),CreatedAt=h.Clock.GetUtcNow()}));await db.SaveChangesAsync(Ct);});
        await using var scope=h.Provider.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<IPlanningAgentService>();
        var turn=await service.SendAgentTurnAsync(user.Id,new(Guid.NewGuid(),Guid.NewGuid(),"Tìm Báo cáo hiếm",false),Ct);
        using var context=JsonDocument.Parse(ai.Context);var root=context.RootElement;
        Assert.True(root.GetProperty("scope").GetProperty("tasks").GetProperty("truncated").GetBoolean());
        Assert.Contains(root.GetProperty("tasks").EnumerateArray(),x=>x.GetProperty("id").GetGuid()==target.Id);
        Assert.Equal(new[]{"Báo cáo A","Báo cáo B"},turn.Options);Assert.NotEmpty(turn.Warnings);
        Assert.Equal(121,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));
    }
    [Fact] public void Usage_counts_only_provider_metadata_and_includes_reported_thinking_tokens()
    {
        var collector=new AiUsageCollector();using var doc=JsonDocument.Parse("""{"modelVersion":"version-1","usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":20,"thoughtsTokenCount":5}}""");
        collector.Read(doc.RootElement);Assert.Equal(10,collector.InputTokens);Assert.Equal(25,collector.OutputTokens);Assert.Equal("version-1",collector.ModelVersion);
        collector.Reset();Assert.Null(collector.InputTokens);Assert.Null(collector.OutputTokens);
    }
}
