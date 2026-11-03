using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<CalendarMoveResult> MoveCalendarItemAsync(Guid uid, CalendarMoveInput input, CancellationToken ct)
    {
        var moved = await Mutate(uid, async () => {
            Require(input.Kind is "Event" or "Session" && input.Id != Guid.Empty && input.Start >= Now && input.End > input.Start && input.End-input.Start <= TimeSpan.FromDays(31),
                "INVALID_CALENDAR_MOVE", "Chọn khung giờ tương lai hợp lệ.");
            var state = await State(uid,ct);
            Require(state.Preference.Revision==input.Revision,"CALENDAR_STALE","Lịch vừa thay đổi. Hãy tải lại lịch trước khi kéo thả.",409);
            var zone=PlanningEngine.Zone(state.Preference.TimeZoneId);
            state=await CalendarStateAsync(uid,state,PlanningEngine.At(LocalDate(input.Start,state.Preference),0,zone),input.End.AddDays(8),ct);
            DateTimeOffset previousStart,previousEnd; Guid? taskId=null;
            if(input.Kind=="Session") {
                var session=await Find<WorkSession>(uid,input.Id,ct);previousStart=session.Start;previousEnd=session.End;taskId=session.TaskId;
                Require(session.Start==input.ExpectedStart && session.End==input.ExpectedEnd,"CALENDAR_STALE","Phiên đã thay đổi. Hãy tải lại lịch.",409);
                Require(session.Status==WorkSessionStatus.Scheduled && !session.IsLocked && session.Start>=Now && session.ActualSeconds==0 && session.SegmentStartedAt is null,
                    "SESSION_NOT_EDITABLE","Chỉ kéo phiên chưa bắt đầu và chưa khóa.",409);
                var task=await Find<WorkTask>(uid,session.TaskId,ct);
                await ValidateExtraTimeTask(uid,task,ct);
                Require(input.End<=task.Deadline,"DEADLINE_CONFLICT","Phiên mới không được vượt deadline của công việc.",409);
                ValidateSession(state,session.TaskId,input.Start,input.End,Occupied(state),session.Id,false);
                session.Start=input.Start;session.End=input.End;session.ReasonCode="USER_ADJUSTMENT";
            } else {
                var entry=await Find<FixedEvent>(uid,input.Id,ct);previousStart=entry.Start;previousEnd=entry.End;
                Require(entry.Start==input.ExpectedStart && entry.End==input.ExpectedEnd,"CALENDAR_STALE","Sự kiện đã thay đổi. Hãy tải lại lịch.",409);
                Require(entry.Start>=Now,"EVENT_NOT_EDITABLE","Không kéo sự kiện đã bắt đầu.",409);
                // Move only this occurrence, preserving its metadata and urgency.
                var occurrences=EventOccurrences(uid,new(entry.Name,input.Start,input.End,entry.Description,entry.EventType,1,entry.IsUrgent),state.Preference);
                ValidateEventConflicts(state,occurrences,entry.Id);
                entry.Start=input.Start;entry.End=input.End;
            }
            await AddNotification(uid,"SCHEDULE_CHANGED","Đã điều chỉnh lịch",input.Kind=="Event"?"Đã cập nhật giờ của một sự kiện.":"Đã cập nhật giờ của một phiên công việc.","calendar-move:"+Guid.NewGuid(),taskId,ct);
            return new CalendarMoveResult(input.Kind,input.Id,input.Start,input.End,previousStart,previousEnd,Guid.Empty);
        },ct);
        // Return the revision actually committed by Mutate for a guarded undo.
        return moved with {Revision=(await Preference(uid,ct)).Revision};
    }
}
