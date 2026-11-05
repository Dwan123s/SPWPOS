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
public sealed record EisenhowerTaskView(Guid Id, string Name, DateTimeOffset? Deadline, string Importance, string Urgency,
    string Quadrant, string Explanation, string[] ReasonCodes, int RemainingMinutes, int? CapacityMinutes,
    bool AtRisk, bool Overdue, bool Blocked, bool NeedsClarification, string Kind = "Personal", Guid? TeamTaskId = null,
    Guid? TeamId = null, Guid Version = default, long Order = 0, TaskChecklistItem[]? Checklist = null,
    string UrgencySource = "Auto", string Recommendation = "Clarify", string Description = "", int EstimatedMinutes = 0,
    int Progress = 0, bool CanEdit = false, bool Splittable = true, bool RequiresContinuousSession = false);
public sealed record EisenhowerView(EisenhowerTaskView[] Tasks, DateTimeOffset EvaluatedAt, DateTimeOffset NextEvaluationAt,
    string AssessmentRevision, string PolicyVersion = "eisenhower-v1", bool Enabled = true, Guid Revision = default);
public interface IEisenhowerService
{
    Task<EisenhowerView> GetEisenhowerAsync(Guid uid, CancellationToken ct);
    Task<EisenhowerTaskView> GetTaskEisenhowerAsync(Guid uid, Guid id, bool team, CancellationToken ct);
    Task SetImportanceAsync(Guid uid, Guid id, bool team, ImportanceInput input, CancellationToken ct);
    Task<ImportanceProposalView> ProposeImportanceAsync(Guid uid, Guid id, bool team, ImportanceProposalInput input, CancellationToken ct);
    Task<Guid> SaveMatrixTaskAsync(Guid uid, Guid? id, MatrixTaskInput input, CancellationToken ct);
    Task<Guid> SaveMatrixDraftAsync(Guid uid, Guid? id, MatrixDraftInput input, CancellationToken ct);
    Task MoveMatrixDraftAsync(Guid uid, Guid id, MatrixMoveInput input, CancellationToken ct);
    Task UpdateMatrixChecklistAsync(Guid uid, Guid id, MatrixChecklistInput input, CancellationToken ct);
    Task MoveMatrixTaskAsync(Guid uid, Guid id, MatrixMoveInput input, CancellationToken ct);
    Task<PlanView> SuggestMatrixScheduleAsync(Guid uid, Guid id, MatrixScheduleInput input, CancellationToken ct);
}

public sealed record TaskChecklistItem(Guid Id, string Text, bool Completed = false);
public sealed record MatrixDraftInput([Required, StringLength(120)] string Name, [StringLength(4000)] string Description = "",
    DateTimeOffset? Deadline = null, [RegularExpression("^(Q[1-4]|Unknown)$")] string Quadrant = "Unknown", Guid Version = default, Guid RequestId = default);
public sealed record MatrixTaskInput([Required, StringLength(200)] string Name, [StringLength(4000)] string Description,
    DateTimeOffset Deadline, [Range(1, 43200)] int EstimatedMinutes, string Quadrant = "Auto", Guid Version = default,
    TaskChecklistItem[]? Checklist = null, bool Splittable = true);
public sealed record MatrixChecklistInput(Guid Version, [Required, MaxLength(100)] TaskChecklistItem[] Items);
public sealed record MatrixMoveInput(Guid Version, Guid Revision, string Quadrant, Guid? BeforeId = null);
public sealed record MatrixScheduleInput(Guid Version, Guid Revision, bool ExplicitRequest = false);
