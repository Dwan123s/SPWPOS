using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record AgentTurnInput(Guid ConversationId, Guid RequestId,
    [Required, StringLength(2000)] string Text, bool AutoApply = true,
    bool UseCapacityFactor = false, bool UseEstimateBias = false);
public sealed record AgentTaskCommand(Guid? TaskId, string Name, DateTimeOffset? Deadline,
    int? EstimatedMinutes, int? Priority, string TaskType, bool? Splittable,
    DateTimeOffset? WorkWindowStart = null, DateTimeOffset? WorkWindowEnd = null,
    string? DurationEvidence = null, string? DeadlineEvidence = null, string? WorkWindowEvidence = null,
    bool DeadlineFromWorkWindow = false);
public sealed record AgentPendingRequest(string OriginalText, Guid? TaskId, DateTimeOffset? WorkWindowStart,
    DateTimeOffset? WorkWindowEnd, int? EstimatedMinutes, DateTimeOffset? Deadline, string[]? Answers = null);
public sealed record AgentExceptionCommand(DateOnly Date, int StartMinute, int EndMinute,
    bool IsAvailable, string Note = "", Guid? ExistingId = null);
public sealed record AgentEventCommand(string Name, DateTimeOffset Start, DateTimeOffset End,
    string Description = "", string EventType = "OTHER", int RepeatWeeks = 1, Guid? ExistingId = null);
public sealed record AgentFreeTimeQuery(DateTimeOffset Start, DateTimeOffset End);
public sealed record AgentIntent(string Reply, string Question, bool ReplaceWeeklyAvailability,
    AvailabilityInput[] WeeklySlots, AgentExceptionCommand[] Exceptions, AgentEventCommand[] Events,
    AgentTaskCommand[] Tasks, Guid[] ScheduleTaskIds, bool Reschedule, int HorizonDays, string[]? Options = null,
    AgentPendingRequest? Pending = null, string? ResultKind = null, AgentFreeTimeQuery? FreeTimeQuery = null,
    AgentFreeTimeQuery? ScheduleReviewQuery = null);
public sealed record AgentMessage(string Text, string Reply, string Status);
public sealed record AgentChangeView(string Kind, string Name, string Before, string After, Guid? TaskId = null, string Reason = "");
public sealed record AgentTurnView(Guid Id, Guid ConversationId, Guid RequestId, string Text,
    string Reply, string Status, string Question, PlanView? Plan, AgentChangeView[] Changes,
    string[] Warnings, DateTimeOffset CreatedAt, bool CanUndo = false, string[]? Options = null, string? ErrorCode = null,
    string Outcome = "");
public sealed record AgentStreamEvent(string Type, string? Stage = null, string? Message = null,
    string? Delta = null, AgentTurnView? Turn = null, string? Code = null);
public sealed record ConversationView(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    bool Archived, int TurnCount, Guid Version);
public sealed record ConversationEdit([Required, StringLength(120)] string Title, bool Archived, Guid Version);
public sealed record ConversationPage(ConversationView[] Items, string? NextCursor);
public sealed record AgentTurnPage(AgentTurnView[] Items, string? NextCursor);
public sealed record ConversationExport(ConversationView Conversation, AgentTurnView[] Turns, bool Truncated);
public interface IPlanningAgentInterpreter
{
    NoteAgentCapabilities Capabilities { get; }
    Task<AgentIntent> InterpretAsync(string text, string contextJson, AgentMessage[] history,
        string timeZoneId, DateTimeOffset now, CancellationToken ct);
    async Task<AgentIntent> InterpretStreamingAsync(string text, string contextJson, AgentMessage[] history,
        string timeZoneId, DateTimeOffset now, Func<string, CancellationToken, Task> onReplyDelta, CancellationToken ct)
    {
        var result = await InterpretAsync(text, contextJson, history, timeZoneId, now, ct);
        await onReplyDelta(result.Reply, ct);
        return result;
    }
}
public interface IPlanningAgentService
{
    Task<ConversationPage> GetConversationsAsync(Guid uid, string? search, bool archived, string? cursor, int pageSize, CancellationToken ct);
    Task<AgentTurnPage> GetConversationTurnsAsync(Guid uid, Guid conversationId, string? cursor, int pageSize, CancellationToken ct);
    Task<ConversationView> EditConversationAsync(Guid uid, Guid conversationId, ConversationEdit input, CancellationToken ct);
    Task DeleteConversationAsync(Guid uid, Guid conversationId, Guid version, CancellationToken ct);
    Task<ConversationExport> ExportConversationAsync(Guid uid, Guid conversationId, CancellationToken ct);
    NoteAgentCapabilities GetAgentCapabilities();
    Task<AgentTurnView[]> GetAgentTurnsAsync(Guid uid, CancellationToken ct);
    Task<AgentTurnView> SendAgentTurnAsync(Guid uid, AgentTurnInput input, CancellationToken ct);
    Task<AgentTurnView> SendAgentTurnStreamingAsync(Guid uid, AgentTurnInput input,
        Func<AgentStreamEvent, CancellationToken, Task> publish, CancellationToken ct);
    Task<AgentTurnView> ApplyAgentTurnAsync(Guid uid, Guid id, CancellationToken ct);
    Task<AgentTurnView> UndoAgentTurnAsync(Guid uid, Guid id, CancellationToken ct);
}
