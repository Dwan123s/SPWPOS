using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PlanningAgentTests
{
    [Theory][InlineData("Event")][InlineData("WorkSession")]
    public async Task Explicit_calendar_kind_never_saves_the_opposite_item_type(string kind)
    {
        var ai=new AgentAiTestDouble {Response=now=>kind=="Event" ? Report(now) : AgentAiTestDouble.Empty() with {Events=[new("Tự học",now.AddHours(2),now.AddHours(3))]}};
        await using var h=await Harness(ai);var user=await h.RegisterAsync();await Setup(h,user.Id);
        var turn=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,Request("Tự học 120 phút, hạn 18:00 hôm nay") with {CalendarKind=kind},Ct));
        Assert.Equal("NEEDS_INPUT",turn.Status);Assert.Empty(turn.Changes);Assert.Null(turn.Plan);
        Assert.Equal(0,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
    }
    [Fact] public async Task Explicit_work_session_creates_task_and_sessions_keeps_raw_chat_and_checks_retry_kind()
    {
        var ai=new AgentAiTestDouble {Response=now=>Report(now)};await using var h=await Harness(ai);var user=await h.RegisterAsync();await Setup(h,user.Id);
        var input=Request("Làm báo cáo SWP 120 phút, hạn 18:00 hôm nay") with{CalendarKind="WorkSession"};
        var turn=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));Assert.Equal("APPLIED",turn.Status);Assert.Equal(input.Text,turn.Text);
        Assert.Equal(1,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));Assert.True(await h.DbAsync(db=>db.WorkSessions.AnyAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));
        Assert.Equal(turn.Id,(await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct))).Id);
        Assert.Equal("AGENT_REQUEST_REUSED",(await Assert.ThrowsAsync<AuthException>(()=>Agent(h,s=>s.SendAgentTurnAsync(user.Id,input with{CalendarKind="Event"},Ct)))).Code);
    }
    [Fact] public async Task Fixed_event_needs_no_availability_creates_no_task_and_can_be_undone()
    {
        var ai=new AgentAiTestDouble {Response=now=>AgentAiTestDouble.Empty() with {Events=[new("Khám nha khoa",now.AddHours(2),now.AddHours(3),EventType:"APPOINTMENT")]}};
        await using var h=await Harness(ai);var user=await h.RegisterAsync();var input=Request("Khám nha khoa hôm nay 10:00–11:00") with{CalendarKind="Event"};
        var turn=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));Assert.Equal("APPLIED",turn.Status);
        Assert.Equal(1,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));Assert.Equal(0,await h.DbAsync(db=>db.WorkSessions.CountAsync(Ct)));
        await Agent(h,s=>s.UndoAgentTurnAsync(user.Id,turn.Id,Ct));Assert.Equal(0,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));
    }
    [Fact] public async Task Auto_mode_allows_an_event_and_task_in_the_same_request_and_invalid_kind_is_rejected_before_AI()
    {
        var ai=new AgentAiTestDouble {Response=now=>Report(now) with {Events=[new("Họp",now.AddHours(3),now.AddHours(4),EventType:"MEETING")]}};
        await using var h=await Harness(ai);var user=await h.RegisterAsync();await Setup(h,user.Id);
        var input=Request("Làm báo cáo SWP 120 phút hạn 18:00 và họp 11:00–12:00 hôm nay");
        var turn=await Agent(h,s=>s.SendAgentTurnAsync(user.Id,input,Ct));Assert.Equal("APPLIED",turn.Status);
        Assert.Equal(1,await h.DbAsync(db=>db.FixedEvents.CountAsync(Ct)));Assert.Equal(1,await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));
        var calls=ai.Calls;await Assert.ThrowsAsync<AuthException>(()=>Agent(h,s=>s.SendAgentTurnAsync(user.Id,input with{RequestId=Guid.NewGuid(),CalendarKind="Other"},Ct)));Assert.Equal(calls,ai.Calls);
    }
}
