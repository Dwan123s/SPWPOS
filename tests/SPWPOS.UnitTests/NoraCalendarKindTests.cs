using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
namespace SPWPOS.UnitTests;

public sealed partial class NoraAgentRuntimeTests
{
    [Theory][InlineData("Event")][InlineData("WorkSession")]
    public async Task Native_agent_receives_type_mismatch_and_cannot_commit_the_opposite_calendar_item(string kind)
    {
        var node=JsonNode.Parse(Draft)!;
        if(kind=="WorkSession"){
            node["tasks"]=new JsonArray();
            node["events"]=JsonNode.Parse("""[{"name":"Tự học","start":"2026-11-01T08:00:00Z","end":"2026-11-01T09:00:00Z","description":"","eventType":"OTHER","repeatWeeks":1,"existingId":null}]""");
        }
        var mismatchObserved=false;
        var model=new Model {Decision=(input,round)=>{
            if(round==1)return Task.FromResult(Call("propose_changes",new{intent=Data(node),strategy="keep_existing"},round));
            mismatchObserved=input.Contents[^1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("response").GetProperty("errorCode").GetString()=="CLARIFICATION_REQUIRED";
            return Task.FromResult(Call("ask_user",new{reply="Chưa lưu thay đổi.",question="Bạn kiểm tra lại loại lịch đã chọn nhé?",options=Array.Empty<string>()},round));
        }};
        await using var h=await Harness(model);var user=await h.RegisterAsync();await Setup(h,user.Id);
        var turn=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,new(Guid.NewGuid(),Guid.NewGuid(),Goal,CalendarKind:kind),Ct));
        Assert.True(mismatchObserved);Assert.Equal("NEEDS_INPUT",turn.Status);Assert.Empty(turn.Changes);
        Assert.Equal(0,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
    }
}
