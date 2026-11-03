using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record TeamBriefFile([Required, StringLength(180)] string Name, [Required, StringLength(100)] string MimeType,
    [Required, StringLength(11184812)] string Data);
public sealed record TeamBriefInput([StringLength(12000)] string Text = "", TeamBriefFile? File = null);
public sealed record TeamBriefTask(string Name, string Summary, string[] Requirements, string[] Deliverables,
    string[] AcceptanceCriteria, string[] Notes, string[] SourceReferences, int EstimatedMinutes,
    DateTimeOffset? Deadline, string DeadlineEvidence, ImportanceSuggestion? Importance = null);
public sealed record TeamBriefAnalysis(string Summary, string Question, string SplitReason, TeamBriefTask[] Tasks, string[] Warnings);
public sealed record TeamBriefContext(int MemberCount, string TimeZoneId, DateTimeOffset Now, string? FileName, string Text, string? DocumentText, string? PdfData);
public interface ITeamBriefInterpreter
{
    Task<TeamBriefAnalysis> AnalyzeTeamBriefAsync(TeamBriefContext context, CancellationToken ct);
}
public sealed record TeamBriefDraft(Guid ProposalId, string Summary, string Question, string SplitReason, int MemberCount,
    string? FileName, SharedTaskView[] Tasks, string[] Warnings, TeamBriefPendingTask[]? PendingTasks = null, Guid Revision = default);
public sealed record TeamBriefPendingTask(Guid Id, string Name, string Description, int EstimatedMinutes,
    DateTimeOffset? Deadline, bool DeadlineNeedsConfirmation, ImportanceView? Importance = null);
public sealed record TeamBriefConfirmView(Guid[] TaskIds);
public sealed record TeamBriefRecipient(Guid TaskId, Guid? UserId);
public sealed record TeamBriefConfirmInput([MaxLength(20)] TeamBriefRecipient[]? Recipients = null, Guid? Revision = null);
public sealed record TeamDraftTaskEdit(Guid Id, [Required, StringLength(200)] string Name,
    [Required, StringLength(4000)] string Description, [Range(1,43200)] int EstimatedMinutes, DateTimeOffset? Deadline);
public sealed record TeamDraftEditInput(Guid Revision, [Required, MinLength(1), MaxLength(20)] TeamDraftTaskEdit[] Tasks);
