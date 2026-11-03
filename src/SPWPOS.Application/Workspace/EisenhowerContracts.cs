using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed class EisenhowerOptions
{
    public bool Enabled { get; set; } = true;
    public bool SchedulingEnabled { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
}
public sealed record ImportanceSuggestion(string Level = "Unknown", string Reason = "", double Confidence = 0, string Evidence = "");
public sealed record ImportanceContext(string Name, string Description, string[] Dependencies);
public sealed record ImportanceProposalInput(Guid Version);
public sealed record ImportanceProposalView(Guid Version, ImportanceSuggestion Suggestion);
public interface IImportanceInterpreter
{
    Task<ImportanceSuggestion> SuggestImportanceAsync(ImportanceContext context, CancellationToken ct);
}
public sealed record ImportanceView(string Level, string Source, string Reason, double? Confidence, bool Confirmed = false);
public sealed record ImportanceInput(Guid Version,
    [Required, RegularExpression("^(Important|Lower|Unknown)$")] string Level,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record EisenhowerTaskView(Guid Id, string Name, DateTimeOffset Deadline, string Importance, string Urgency,
    string Quadrant, string Explanation, string[] ReasonCodes, int RemainingMinutes, int? CapacityMinutes,
    bool AtRisk, bool Overdue, bool Blocked, bool NeedsClarification, string Kind = "Personal", Guid? TeamTaskId = null,
    Guid? TeamId = null);
public sealed record EisenhowerView(EisenhowerTaskView[] Tasks, DateTimeOffset EvaluatedAt, DateTimeOffset NextEvaluationAt,
    string AssessmentRevision, string PolicyVersion = "eisenhower-v1", bool Enabled = true);
public interface IEisenhowerService
{
    Task<EisenhowerView> GetEisenhowerAsync(Guid uid, CancellationToken ct);
    Task<EisenhowerTaskView> GetTaskEisenhowerAsync(Guid uid, Guid id, bool team, CancellationToken ct);
    Task SetImportanceAsync(Guid uid, Guid id, bool team, ImportanceInput input, CancellationToken ct);
    Task<ImportanceProposalView> ProposeImportanceAsync(Guid uid, Guid id, bool team, ImportanceProposalInput input, CancellationToken ct);
}
