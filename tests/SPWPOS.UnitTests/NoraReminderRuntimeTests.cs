using Microsoft.EntityFrameworkCore;
using SPWPOS.TestSupport;
using SPWPOS.Application.Workspace;

namespace SPWPOS.UnitTests;

public sealed partial class NoraAgentRuntimeTests
{
    [Fact]
    public async Task Native_reminder_tool_saves_once_without_calendar_simulation_or_Inbox_creation()
    {
        var model=new Model {SupportsRequestUnderstanding=true,Decision=(request,number)=>{
            Assert.Contains(request.Tools,tool=>tool.Name=="manage_reminder");
            return Task.FromResult(Call("manage_reminder",new {operation="Create",message="Rep mail khách hàng",at=(string?)null,reminderId=(string?)null},number)
                with {Understanding=new("Mutation","Đặt nhắc trả lời email sau một phút.",false,[])});
        }};
        await using var h=await Harness(model);var user=await h.RegisterAsync();var now=h.Clock.GetUtcNow();
        var input=new AgentTurnInput(Guid.NewGuid(),Guid.NewGuid(),"1 phút nữa nhắc tôi rep mail khách hàng");
        var result=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));
        Assert.Equal("APPLIED",result.Status);Assert.Equal("manage_reminder",Assert.Single(result.Run!.Steps).Tool);
        Assert.Equal("APPLIED",result.Run.Status);Assert.Equal(1,model.Calls);
        var row=await h.DbAsync(db=>db.InAppNotifications.SingleAsync(Ct));
        Assert.Equal("NORA_REMINDER",row.Kind);Assert.Equal(now.AddMinutes(1),row.ScheduledFor);
        Assert.False(await h.DbAsync(db=>db.PlanningNotes.AnyAsync(Ct)));Assert.False(await h.DbAsync(db=>db.WorkTasks.AnyAsync(Ct)));
        await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));Assert.Equal(1,await h.DbAsync(db=>db.InAppNotifications.CountAsync(Ct)));
    }
    [Fact]
    public async Task Read_only_reminder_intent_cannot_create_an_alarm()
    {
        var model=new Model {SupportsRequestUnderstanding=true,Decision=(_,number)=>Task.FromResult(
            Call("manage_reminder",new {operation="Create",message="Không được lưu",at="2026-10-31T10:00:00Z",reminderId=(string?)null},number)
            with {Understanding=new("TaskQuery","Xem các lời nhắc đã lưu.",true,[])})};
        await using var h=await Harness(model,rounds:2);var user=await h.RegisterAsync();
        var result=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,new(Guid.NewGuid(),Guid.NewGuid(),"Tôi đang có lời nhắc nào?"),Ct));
        Assert.NotEqual("APPLIED",result.Status);Assert.False(await h.DbAsync(db=>db.InAppNotifications.AnyAsync(Ct)));
    }
}
