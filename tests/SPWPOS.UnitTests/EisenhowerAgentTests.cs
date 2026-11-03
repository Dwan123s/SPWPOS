using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    [Fact] public async Task Importance_is_proposed_in_the_existing_AI_call_and_only_metadata_correction_never_replans_sessions()
    {
        var ai=new AgentAiTestDouble { Response=now=>Report(now) with { Tasks=[Report(now).Tasks[0] with {
            Importance=new("Important","Đầu ra bắt buộc cho môn học",.95,"bắt buộc") }] } };
        await using var h=await Harness(ai); var user=await h.RegisterAsync(); await Setup(h,user.Id);
        var created=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Request("Báo cáo bắt buộc, 120 phút, hạn hôm nay",true),Ct));
        Assert.Equal("APPLIED",created.Status); Assert.Equal(1,ai.Calls);
        var task=await h.DbAsync(db=>db.WorkTasks.SingleAsync(Ct)); Assert.Equal("Important",task.ImportanceLevel);
        var sessions=await h.DbAsync(db=>db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        ai.Response=_=>AgentAiTestDouble.Empty() with { Tasks=[new(task.Id,task.Name,null,null,null,"",null,
            Importance:new("Lower","Công việc hỗ trợ",.95,"ít quan trọng"))] };
        var corrected=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Request("Công việc này ít quan trọng, chỉ sửa mức quan trọng và giữ nguyên lịch",true),Ct));
        Assert.Equal("APPLIED",corrected.Status); Assert.Null(corrected.Plan); Assert.Equal(2,ai.Calls);
        var after=await h.DbAsync(db=>db.WorkSessions.AsNoTracking().ToArrayAsync(Ct));
        Assert.Equal(sessions.Select(s=>(s.Id,s.Start,s.End,s.Status)),after.Select(s=>(s.Id,s.Start,s.End,s.Status)));
        Assert.Equal("Lower",(await h.DbAsync(db=>db.WorkTasks.SingleAsync(Ct))).ImportanceLevel);
    }
}
