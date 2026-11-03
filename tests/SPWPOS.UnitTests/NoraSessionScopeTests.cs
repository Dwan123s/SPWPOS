using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;
public sealed partial class NoraAgentRuntimeTests
{
    [Fact] public async Task Native_session_clock_reference_does_not_turn_local_collision_repair_into_global_replanning()
    {
        var node = JsonNode.Parse(Draft)!; node["tasks"]=new JsonArray(); node["reschedule"]=true;
        node["events"]=JsonNode.Parse("""[{"name":"Kiểm tra","start":"2026-11-01T14:00:00Z","end":"2026-11-01T15:00:00Z","description":"","eventType":"APPOINTMENT","repeatWeeks":1,"existingId":null}]""");
        var model=new Model {Decision=(_,n)=>Task.FromResult(Call("propose_changes",new {intent=Data(node),strategy="replan_flexible"},n))};
        await using var h=await Harness(model); var user=await h.RegisterAsync(); await Setup(h,user.Id);
        var a=new WorkTask {UserId=user.Id,Name="Phần việc được dời",EstimatedMinutes=140,Deadline=DateTimeOffset.Parse("2026-11-02T22:00:00Z")};
        var b=new WorkTask {UserId=user.Id,Name="Việc khác",EstimatedMinutes=60,Deadline=a.Deadline};
        var work=new WorkSession {UserId=user.Id,TaskId=a.Id,Start=DateTimeOffset.Parse("2026-11-01T12:45:00Z"),End=DateTimeOffset.Parse("2026-11-01T14:05:00Z")};
        var other=new WorkSession {UserId=user.Id,TaskId=b.Id,Start=DateTimeOffset.Parse("2026-11-01T17:00:00Z"),End=DateTimeOffset.Parse("2026-11-01T18:00:00Z")};
        var sameTaskOtherSession=new WorkSession {UserId=user.Id,TaskId=a.Id,Start=DateTimeOffset.Parse("2026-11-01T19:00:00Z"),End=DateTimeOffset.Parse("2026-11-01T20:00:00Z")};
        await h.DbAsync(async db=>{db.WorkTasks.AddRange(a,b);db.WorkSessions.AddRange(work,other,sameTaskOtherSession);await db.SaveChangesAsync(Ct);});
        node["scheduleTaskIds"]=new JsonArray(a.Id.ToString(),b.Id.ToString());
        var turn=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,new(Guid.NewGuid(),Guid.NewGuid(),"Dời phiên làm việc 12:45–14:05 để kiểm tra ngày mai lúc 14:00–15:00, giữ các việc khác.",false),Ct));
        Assert.Equal("PREVIEW",turn.Status);Assert.Equal(1,model.Calls);
        Assert.Contains(turn.Plan!.Sessions,s=>s.Id==other.Id && s.Preserved);
        Assert.Contains(turn.Plan.Sessions,s=>s.Id==sameTaskOtherSession.Id && s.Preserved && s.Start==sameTaskOtherSession.Start);
        Assert.False((await h.DbAsync(db=>db.WorkSessions.SingleAsync(s=>s.Id==sameTaskOtherSession.Id,Ct))).IsLocked);
        Assert.All(turn.Plan.Sessions.Where(s=>s.TaskId==a.Id),s=>Assert.True(s.End<=work.End.AddMinutes(-5) || s.Start>=work.End.AddMinutes(55)));
        Assert.Equal(0,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));
    }
}
