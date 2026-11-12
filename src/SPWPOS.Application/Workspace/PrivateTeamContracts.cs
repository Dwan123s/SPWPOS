using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record TeamNameInput([Required, StringLength(120, MinimumLength = 1)] string Name);
public sealed record TeamTextInput([Required, StringLength(2000, MinimumLength = 1)] string Text);
public sealed record TeamVersionInput(Guid Version);
public sealed record TeamJoinReviewInput(Guid Version, bool Approved);
public sealed record TeamJoinRequestView(Guid UserId, string Name, string Email, Guid Version, DateTimeOffset? RequestedAt);
public sealed record MyTeamJoinRequestView(Guid TeamId, string TeamName, string Status, DateTimeOffset? RequestedAt);
public sealed record TeamTaskRecipientInput(Guid? UserId = null);
public sealed record TeamTaskRecipientChangeInput(Guid Version, Guid? UserId);
public sealed record TeamProgressInput(Guid Version, [Range(0, 100)] int Progress);
public sealed record TeamCompletionReviewInput(Guid Version, Guid RequestId, bool Approved, [StringLength(500)] string? Note = null);
public sealed record TeamCompletionFileInput([Required, StringLength(180)] string Name, [Required, StringLength(11184812)] string Data);
public sealed record TeamCompletionSubmitInput(Guid Version, Guid RequestId, [StringLength(1000)] string? Note = null,
    [MaxLength(3)] TeamCompletionFileInput[]? Files = null);
public sealed record TeamCompletionFileView(Guid Id, string Name, string MimeType, int Size, DateTimeOffset CreatedAt);
public sealed record TeamCompletionDownload(string Name, string MimeType, byte[] Content);
public sealed record TeamSourceFileView(Guid Id, string Name, string MimeType, int Size, DateTimeOffset CreatedAt);
public sealed record TeamSourceFileInput(Guid Version, Guid RequestId, [Required] TeamBriefFile File);
public sealed record TeamView(Guid Id, string Name, string Role);
public sealed record TeamMemberView(Guid UserId, string Name, string Role, Guid Version, string AvatarUrl = "");
public sealed record SharedTaskView(Guid Id, string Name, string Description, int EstimatedMinutes, DateTimeOffset Deadline,
    int Priority, bool Splittable, Guid? AssigneeId, int Progress, string Status, Guid Version, bool HasPendingChange,
    Guid? CompletionRequestId = null, DateTimeOffset? CompletionSubmittedAt = null, DateTimeOffset? CompletionReviewedAt = null,
    Guid? CompletionReviewedBy = null, bool? CompletionApproved = null, string? CompletionReviewNote = null,
    string? CompletionSubmissionNote = null, TeamCompletionFileView[]? CompletionFiles = null, TeamSourceFileView[]? SourceFiles = null,
    Guid? DesignatedUserId = null, ImportanceView? Importance = null, Guid? ParentTaskId = null, TaskChecklistItem[]? Checklist = null,
    int SubtasksCompleted = 0, int SubtaskCount = 0);
public sealed record TeamActivityView(Guid ActorId, Guid? TaskId, string Kind, DateTimeOffset CreatedAt);
public sealed record TeamDetailView(TeamView Team, TeamMemberView[] Members, SharedTaskView[] Tasks, TeamActivityView[] Activity, TeamJoinRequestView[]? JoinRequests = null);
public sealed record TeamInviteView(Guid Id, string Code, DateTimeOffset ExpiresAt);
public sealed record TeamInvitePreview(string TeamName, int MemberCount, DateTimeOffset ExpiresAt, bool AlreadyMember, string? RequestStatus = null);
public sealed record ManualTeamTaskInput(string Name, string? Description, int EstimatedMinutes,
    DateTimeOffset Deadline, int Priority = 2, bool Splittable = true);
public sealed record ManualTeamTaskChangeInput(Guid Version, ManualTeamTaskInput Task);
public sealed record TeamSubtaskInput(string Name, int EstimatedMinutes, string[] Requirements, string? Notes = null, Guid? DesignatedUserId = null);
public sealed record TeamTaskSplitInput(Guid Version, [Required, MinLength(2), MaxLength(2)] TeamSubtaskInput[] Children);
public sealed record TeamChecklistToggleInput(Guid Version, bool Completed);
public sealed record TeamTaskDraft(Guid ProposalId, string Reply, string Question, SharedTaskView? Task);
public sealed record PrivateClaimSession(Guid Id, string TaskName, DateTimeOffset Start, DateTimeOffset End, bool Preserved);
public sealed record PrivateClaimOption(int Index, string Label, PrivateClaimSession[] After, PrivateClaimSession[] Before, string[] Explanations);
public sealed record ClaimFailureView(string Code, int RequiredMinutes, int FreeMinutes, int FocusMinutes, int CapacityMinutes, int MissingMinutes, string[] Blockers, bool NeedsAvailability = false);
public sealed record PrivateClaimView(Guid Id, Guid TaskId, DateTimeOffset ExpiresAt, PrivateClaimOption[] Options, string[] Warnings, bool CanUndo = true, string Analysis = "", ClaimFailureView? Failure = null);
public sealed record TeamReturnPreview(Guid TaskId, Guid Version, Guid Revision, PrivateClaimSession[] Sessions);
public sealed record TeamReturnInput(Guid Version, Guid Revision);
public sealed record TeamChatInput(Guid RequestId, [Required, StringLength(2000, MinimumLength = 1)] string Text);
public sealed record TeamChatMessageView(long Id, Guid UserId, string Author, string Text, DateTimeOffset CreatedAt, bool IsSystem = false);
public sealed record TeamChatAuthorView(Guid UserId, string Name, string AvatarUrl, string SelectedSkin = "classic", int UserLevel = 1);
public sealed record TeamChatPage(TeamChatMessageView[] Items, long? NextAfter, bool HasMore, TeamChatAuthorView[]? Authors = null);
public sealed record ClaimConfirmInput([Range(0, 2)] int Option = 0);
public sealed record PolicyView(int AwakeStart, int AwakeEnd, int LunchStart, int LunchEnd, int DinnerStart, int DinnerEnd, int CommuteMinutes, int FocusMinutes, bool Confirmed, DateTimeOffset? ConfirmedAt = null, bool HasDailyRoutine = false,
    DateOnly[]? TimetableWeeks = null);
public sealed record RoutineBlock(string Kind, string Label, DateTimeOffset Start, DateTimeOffset End);
public sealed record PolicyDayView(DateOnly Date, int CalendarGapMinutes, TimeSlot[] WorkGaps, TimeSlot[] FocusSlots, int SchedulableMinutes,
    SPWPOS.Domain.Entities.RoutineDay? Routine = null, RoutineBlock[]? Timeline = null);
public sealed record PolicyPreviewView(Guid Id, PolicyView Policy, DateTimeOffset ExpiresAt, PrivateClaimSession[] Before, PrivateClaimSession[] After, string[] Warnings, PolicyDayView[]? Days = null, DateOnly? WeekStart = null, DateOnly? WeekEnd = null);
public interface IPolicyDraftInterpreter
{
    Task<PolicyView> DraftPolicyAsync(string text, PolicyView current, CancellationToken ct);
}
public interface IPrivateTeamService
{
    Task<TeamView[]> ListTeamsAsync(Guid uid, CancellationToken ct);
    Task<TeamWorkloadView> GetTeamWorkloadAsync(Guid uid, Guid teamId, int week, CancellationToken ct);
    Task<TeamChatPage> GetTeamChatAsync(Guid uid, Guid teamId, long? after, long? before, CancellationToken ct);
    Task<TeamChatMessageView> SendTeamChatAsync(Guid uid, Guid teamId, TeamChatInput input, CancellationToken ct);
    Task<Guid> CreateTeamAsync(Guid uid, TeamNameInput input, CancellationToken ct);
    Task<TeamDetailView> GetTeamAsync(Guid uid, Guid teamId, CancellationToken ct);
    Task<TeamInviteView> CreateTeamInviteAsync(Guid uid, Guid teamId, CancellationToken ct);
    Task<TeamInvitePreview> PreviewTeamInviteAsync(Guid uid, string code, CancellationToken ct);
    Task RevokeTeamInviteAsync(Guid uid, Guid teamId, Guid inviteId, CancellationToken ct);
    Task<Guid> JoinTeamAsync(Guid uid, string code, CancellationToken ct);
    Task<MyTeamJoinRequestView[]> ListMyTeamJoinRequestsAsync(Guid uid, CancellationToken ct);
    Task ReviewTeamJoinRequestAsync(Guid uid, Guid teamId, Guid memberId, TeamJoinReviewInput input, CancellationToken ct);
    Task RemoveTeamMemberAsync(Guid uid, Guid teamId, Guid memberId, Guid version, CancellationToken ct);
    Task<TeamTaskDraft> DraftManualTeamTaskAsync(Guid uid, Guid teamId, ManualTeamTaskInput input, CancellationToken ct);
    Task<TeamTaskDraft> DraftTeamTaskAsync(Guid uid, Guid teamId, TeamTextInput input, CancellationToken ct);
    Task<Guid> ConfirmTeamTaskDraftAsync(Guid uid, Guid teamId, Guid proposalId, CancellationToken ct, TeamTaskRecipientInput? recipient = null);
    Task ChangeTeamTaskRecipientAsync(Guid uid, Guid taskId, TeamTaskRecipientChangeInput input, CancellationToken ct);
    Task<Guid[]> SplitTeamTaskAsync(Guid uid, Guid taskId, TeamTaskSplitInput input, CancellationToken ct);
    Task SetTeamChecklistItemAsync(Guid uid, Guid taskId, Guid itemId, TeamChecklistToggleInput input, CancellationToken ct);
    Task<TeamBriefDraft> DraftTeamBriefAsync(Guid uid, Guid teamId, TeamBriefInput input, CancellationToken ct);
    Task<TeamBriefDraft> EditTeamBriefDraftAsync(Guid uid, Guid teamId, Guid proposalId, TeamDraftEditInput input, CancellationToken ct);
    Task<TeamBriefConfirmView> ConfirmTeamBriefAsync(Guid uid, Guid teamId, Guid proposalId, CancellationToken ct, TeamBriefConfirmInput? input = null);
    Task<PrivateClaimView> PreviewTeamClaimAsync(Guid uid, Guid taskId, CancellationToken ct, bool allowExtendedDisplacement = false);
    Task<string?> ExplainTeamClaimAsync(Guid uid, Guid proposalId, CancellationToken ct);
    Task ConfirmTeamClaimAsync(Guid uid, Guid proposalId, ClaimConfirmInput input, CancellationToken ct);
    Task CancelTeamClaimAsync(Guid uid, Guid proposalId, CancellationToken ct);
    Task UndoTeamClaimAsync(Guid uid, Guid proposalId, CancellationToken ct);
    Task<TeamReturnPreview> PreviewTeamReturnAsync(Guid uid, Guid taskId, CancellationToken ct);
    Task ReturnTeamTaskAsync(Guid uid, Guid taskId, Guid version, CancellationToken ct, Guid? revision = null);
    Task UpdateTeamProgressAsync(Guid uid, Guid taskId, TeamProgressInput input, CancellationToken ct);
    Task ReviewTeamCompletionAsync(Guid uid, Guid taskId, TeamCompletionReviewInput input, CancellationToken ct);
    Task SubmitTeamCompletionAsync(Guid uid, Guid taskId, TeamCompletionSubmitInput input, CancellationToken ct);
    Task<TeamCompletionDownload> DownloadTeamCompletionFileAsync(Guid uid, Guid taskId, Guid fileId, CancellationToken ct);
    Task<TeamCompletionDownload> DownloadTeamSourceFileAsync(Guid uid, Guid taskId, Guid fileId, CancellationToken ct);
    Task AttachTeamSourceFileAsync(Guid uid, Guid taskId, TeamSourceFileInput input, CancellationToken ct);
    Task<TeamTaskDraft> ProposeTeamTaskChangeAsync(Guid uid, Guid taskId, TeamTextInput input, CancellationToken ct);
    Task<TeamTaskDraft> ProposeManualTeamTaskChangeAsync(Guid uid, Guid taskId, ManualTeamTaskChangeInput input, CancellationToken ct);
    Task<PrivateClaimView> PreviewTeamTaskChangeAsync(Guid uid, Guid taskId, CancellationToken ct);
    Task RejectTeamTaskChangeAsync(Guid uid, Guid taskId, Guid version, CancellationToken ct);
    Task<PolicyView?> GetAvailabilityPolicyAsync(Guid uid, CancellationToken ct);
    Task<PolicyDayView[]> GetPolicySlotsAsync(Guid uid, CancellationToken ct);
    Task<PolicyPreviewView> PreviewAvailabilityPolicyAsync(Guid uid, CancellationToken ct, Guid? timetableTurnId = null, bool useTimetableHours = false);
    Task<PolicyPreviewView> DraftAvailabilityPolicyAsync(Guid uid, TeamTextInput input, CancellationToken ct, Guid? timetableTurnId = null, bool useTimetableHours = false);
    Task ConfirmAvailabilityPolicyAsync(Guid uid, Guid proposalId, CancellationToken ct);
}
