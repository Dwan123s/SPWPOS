using System.ComponentModel.DataAnnotations;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Application.Workspace;

public sealed record TaskInput(
    [Required, StringLength(200)] string Name,
    [StringLength(4000)] string Description,
    DateTimeOffset Deadline,
    [Range(1, 43200)] int EstimatedMinutes,
    [Range(1, 4)] int Priority = 2,
    [StringLength(60), Required] string TaskType = "General",
    bool Splittable = true,
    [Range(5, 480)] int MinimumSessionMinutes = 25,
    Guid? CategoryId = null, Guid? ParentId = null, bool IsHardDeadline = false,
    [RegularExpression("^(MANUAL|NATURAL_LANGUAGE|AI_BREAKDOWN)$")] string CreationSource = "MANUAL");
public sealed record TaskView(Guid Id, string Name, string Description, DateTimeOffset Deadline, int EstimatedMinutes,
    int AdjustedMinutes, int RemainingMinutes, int ActualMinutes, int PlannedMinutes, int Priority, string TaskType,
    bool Splittable, int MinimumSessionMinutes, Guid? CategoryId, Guid? ParentId, string Status, int Progress,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, Guid[] Dependencies, bool IsHardDeadline = false, string CreationSource = "MANUAL",
    DateTimeOffset? WorkWindowStart = null, DateTimeOffset? WorkWindowEnd = null);
public sealed record CategoryInput([Required, StringLength(80)] string Name,
    [RegularExpression("^#[a-fA-F0-9]{6}$")] string Color = "#6366f1");
public sealed record CategoryView(Guid Id, string Name, string Color);
public sealed record StatusInput([Required] string Status, [Range(0, 100)] int Progress);
public sealed record ProgressInput([Range(0, 100)] int Progress);
public sealed record ProfileInput([Required, StringLength(100)] string FirstName, [Required, StringLength(100)] string LastName, [StringLength(100)] string? DisplayName = null, [StringLength(1000)] string? Bio = null, [StringLength(3000000)] string? AvatarUrl = null);
public sealed record ProfileView(Guid Id, string Email, string FirstName, string LastName, string DisplayName = "", string Bio = "", string AvatarUrl = "");
public sealed record AvailabilityInput([Range(0, 6)] int DayOfWeek, [Range(0, 1439)] int StartMinute, [Range(1, 1440)] int EndMinute);
public sealed record WeeklyInput([Required, MaxLength(28)] AvailabilityInput[] Slots);
public sealed record ExceptionInput(DateOnly Date, [Range(0, 1439)] int StartMinute,
    [Range(1, 1440)] int EndMinute, bool IsAvailable, [StringLength(300)] string Note = "");
public sealed record ExceptionView(Guid Id, DateOnly Date, int StartMinute, int EndMinute, bool IsAvailable, string Note, bool OverridesWeeklyAvailability = false);
public sealed record EventInput([Required, StringLength(200)] string Name, DateTimeOffset Start, DateTimeOffset End,
    [StringLength(1000)] string Description = "", [RegularExpression("^(MEETING|CLASS|APPOINTMENT|OTHER)$")] string EventType = "OTHER",
    [Range(1, 12)] int RepeatWeeks = 1);
public sealed record EventView(Guid Id, string Name, DateTimeOffset Start, DateTimeOffset End, string Description = "", string EventType = "OTHER");
public sealed record PreferenceInput([Required, StringLength(100)] string TimeZoneId,
    [Range(5, 480)] int MinimumSessionMinutes, [Range(5, 480)] int MaximumSessionMinutes,
    [Range(0, 120)] int BreakMinutes, [Range(0, 1439)] int PreferredStartMinute,
    [Range(1, 1440)] int PreferredEndMinute, [Range(1, 30)] int PlanningHorizonDays = 14,
    [RegularExpression("^(MORNING|AFTERNOON|EVENING|NONE)$")] string PreferredPeriod = "NONE",
    [Range(5, 1440)] int MaximumDailyMinutes = 480, bool AutoReschedule = true);
public sealed record PreferenceView(string TimeZoneId, int MinimumSessionMinutes, int MaximumSessionMinutes,
    int BreakMinutes, int PreferredStartMinute, int PreferredEndMinute, Guid Revision, int PlanningHorizonDays = 14, string PreferredPeriod = "NONE",
    int MaximumDailyMinutes = 480, bool AutoReschedule = true);
public sealed record SessionView(Guid Id, Guid TaskId, string TaskName, DateTimeOffset Start, DateTimeOffset End,
    string Status, bool IsLocked, int ActualMinutes, long ActualSeconds, DateTimeOffset? SegmentStartedAt, string ReasonCode);
public sealed record SessionEdit(DateTimeOffset Start, DateTimeOffset End);
public sealed record SplitInput([Range(5, 480)] int FirstMinutes);
public sealed record DurationInput([Range(0, 10080)] int ActualMinutes);
public sealed record PlanInput(DateTimeOffset Start, [Range(1, 30)] int HorizonDays = 7,
    [MaxLength(500)] Guid[]? AffectedTaskIds = null, bool IsReschedule = false,
    [RegularExpression("^(MANUAL|NEW_TASK|TASK_CHANGED|EARLY_COMPLETION|SESSION_MISSED|DEADLINE_CHANGED|AVAILABILITY_CHANGED)$")] string TriggerEvent = "MANUAL",
    bool UseCapacityFactor = true, bool UseEstimateBias = true);
public sealed record PlanWarning(Guid TaskId, string TaskName, string Code, string Message, int UnscheduledMinutes);
public sealed record ProposedSession(Guid Id, Guid TaskId, DateTimeOffset Start, DateTimeOffset End, string ReasonCode, bool Preserved);
public sealed record PlanView(Guid Id, Guid Revision, DateTimeOffset Start, DateTimeOffset End,
    ProposedSession[] Sessions, PlanWarning[] Warnings, string[] Explanations, bool IsApplied,
    string Status = "PROPOSED", string Kind = "INITIAL", Guid? PreviousPlanId = null, DateTimeOffset? AppliedAt = null,
    bool UseEstimateBias = true);
public sealed record ScenarioInput(PlanInput Plan, TaskInput NewTask, [MaxLength(11)] TaskInput[]? AdditionalTasks = null);
public sealed record BiasView(string TaskType, int Samples, double Ratio, bool Personalized);
public sealed record RiskView(Guid TaskId, string Name, string Level, int Score, string[] Reasons, int RemainingMinutes);
public sealed record CapacityDayView(DateOnly Date, int CalendarFreeMinutes, int DailyBudgetMinutes,
    int ConsumedMinutes, int ReservedMinutes, int EffectiveCapacityMinutes, int SchedulableMinutes);
public sealed record IntelligenceView(int RemainingMinutes, int CalendarFreeMinutes, int EffectiveCapacityMinutes,
    double CapacityFactor, int CapacitySamples, double LoadRatio, string RiskLevel, string[] Reasons,
    BiasView[] Biases, RiskView[] Tasks, int PlannedMinutes, int ActualMinutes, int HorizonDemandMinutes = 0,
    CapacityDayView[]? Days = null, DateTimeOffset? RangeStart = null, DateTimeOffset? RangeEnd = null);
public sealed record ScenarioView(IntelligenceView Before, IntelligenceView After, PlanView ProposedPlan, int AddedMinutes);
public sealed record NotificationView(Guid Id, string Kind, string Title, string Message, Guid? TaskId, DateTimeOffset CreatedAt, bool IsRead);
public sealed record WorkspaceView(ProfileView Profile, PreferenceView Preference, CategoryView[] Categories,
    TaskView[] Tasks, AvailabilityInput[] Availability, ExceptionView[] Exceptions, EventView[] Events,
    SessionView[] Sessions, NotificationView[] Notifications, NotificationSettingInput NotificationSettings, string[]? Features = null);
public sealed record NotificationSettingInput(bool InAppEnabled = true, [Range(1, 168)] int DeadlineLeadHours = 24,
    [Range(1, 120)] int SessionReminderLeadMinutes = 10, [RegularExpression("^(MODERATE|HIGH|OVERLOAD)$")] string WorkloadWarningLevel = "HIGH", bool EmailEnabled = false);
public sealed record PlanItemView(Guid Id, Guid TaskId, Guid? PreviousSessionId, string Action, DateTimeOffset? Start, DateTimeOffset? End, int? PlannedMinutes, string[] ReasonCodes);
public sealed record ExecutionSegmentView(DateTimeOffset Start, DateTimeOffset? End);
public sealed record ExecutionView(Guid SessionId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, long TimerSeconds, long ActualSeconds, bool IsUserAdjusted, ExecutionSegmentView[] Segments);
public sealed record AccountStatusInput([StringLength(500)] string? Reason = null);
public sealed record AccountStatusLogView(Guid Id, Guid UserId, Guid AdminId, string Action, string? Reason, DateTimeOffset CreatedAt);
public sealed record JobRunView(Guid Id, Guid? UserId, string JobType, string Status, string? ErrorMessage, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);
public sealed record TaskSuggestion(TaskInput Task, string Source, string[] Notes);
public sealed record CaptureInput([Required, StringLength(2000)] string Text, bool UseAi = false);
public sealed record BreakdownView(TaskInput[] Subtasks, string Source);
public sealed record AdminUserView(Guid Id, string Email, string FirstName, string LastName, bool IsActive, bool EmailConfirmed, DateTimeOffset? LastLoginAt,
    string? UserName = null, DateTimeOffset? CreatedAt = null, int TotalTasks = 0, int CompletedTasks = 0, int ActiveLoginSessions = 0);
public sealed record SystemStatusView(bool DatabaseHealthy, int Users, int Tasks, int ActiveSessions, int FailedLoginsLast24Hours, DateTimeOffset ServerTime, DateTimeOffset? LastNotificationRun, long FailedNotificationRuns);

public interface IWorkspaceService
{
    Task<RecoveryPage> GetRecoveryAsync(Guid userId, int page, CancellationToken ct);
    Task SetRecoverySettingsAsync(Guid userId, RecoverySettingInput input, CancellationToken ct);
    Task ScanRecoveryAsync(Guid userId, CancellationToken ct);
    Task RebuildRecoveryAsync(Guid userId, Guid id, CancellationToken ct);
    Task ApplyRecoveryAsync(Guid userId, Guid id, CancellationToken ct);
    Task DismissRecoveryAsync(Guid userId, Guid id, CancellationToken ct);
    Task<AnalyticsView> GetAnalyticsAsync(Guid userId, AnalyticsInput input, CancellationToken ct);
    Task<TaskPerformancePage> GetTaskPerformanceAsync(Guid userId, int page, int pageSize, string? search, CancellationToken ct);
    NoteAgentCapabilities GetNoteAgentCapabilities();
    Task<NoteView[]> GetNotesAsync(Guid userId, CancellationToken ct);
    Task<NoteView> SaveNoteAsync(Guid userId, Guid? id, NoteInput input, CancellationToken ct);
    Task DeleteNoteAsync(Guid userId, Guid id, Guid version, CancellationToken ct);
    Task<NoteView> AnalyzeNoteAsync(Guid userId, Guid id, NoteAnalysisInput input, CancellationToken ct);
    Task<NoteView> ImportNoteAsync(Guid userId, Guid id, NoteImportInput input, CancellationToken ct);
    Task<NoteScheduleResult> ScheduleNoteAsync(Guid userId, Guid id, NoteScheduleInput input, CancellationToken ct);
    Task<WorkspaceView> GetAsync(Guid userId, CancellationToken ct);
    Task UpdateProfileAsync(Guid userId, ProfileInput input, CancellationToken ct);
    Task<Guid> SaveTaskAsync(Guid userId, Guid? id, TaskInput input, CancellationToken ct);
    Task DeleteTaskAsync(Guid userId, Guid id, CancellationToken ct);
    Task SetStatusAsync(Guid userId, Guid id, StatusInput input, CancellationToken ct);
    Task<Guid> SaveCategoryAsync(Guid userId, Guid? id, CategoryInput input, CancellationToken ct);
    Task DeleteCategoryAsync(Guid userId, Guid id, CancellationToken ct);
    Task SetDependencyAsync(Guid userId, Guid id, Guid predecessor, bool remove, CancellationToken ct);
    Task SetAvailabilityAsync(Guid userId, WeeklyInput input, CancellationToken ct);
    Task<Guid> SaveExceptionAsync(Guid userId, Guid? id, ExceptionInput input, CancellationToken ct);
    Task DeleteExceptionAsync(Guid userId, Guid id, CancellationToken ct);
    Task<Guid> SaveEventAsync(Guid userId, Guid? id, EventInput input, CancellationToken ct);
    Task DeleteEventAsync(Guid userId, Guid id, CancellationToken ct);
    Task SetPreferenceAsync(Guid userId, PreferenceInput input, CancellationToken ct);
    Task<PlanView> GeneratePlanAsync(Guid userId, PlanInput input, CancellationToken ct);
    Task<PlanView> GetPlanAsync(Guid userId, Guid id, CancellationToken ct);
    Task<PlanWarning[]> DetectConflictsAsync(Guid userId, PlanInput input, CancellationToken ct);
    Task ApplyPlanAsync(Guid userId, Guid id, CancellationToken ct);
    Task EditSessionAsync(Guid userId, Guid id, SessionEdit input, CancellationToken ct);
    Task LockSessionAsync(Guid userId, Guid id, bool locked, CancellationToken ct);
    Task SplitSessionAsync(Guid userId, Guid id, SplitInput input, CancellationToken ct);
    Task ExecuteSessionAsync(Guid userId, Guid id, string action, CancellationToken ct);
    Task RecordDurationAsync(Guid userId, Guid id, DurationInput input, CancellationToken ct);
    Task<IntelligenceView> AnalyzeAsync(Guid userId, PlanInput input, CancellationToken ct);
    Task<ScenarioView> ScenarioAsync(Guid userId, ScenarioInput input, CancellationToken ct);
    Task MarkNotificationAsync(Guid userId, Guid id, CancellationToken ct);
    Task SetNotificationSettingsAsync(Guid userId, NotificationSettingInput input, CancellationToken ct);
    Task<PlanItemView[]> GetPlanItemsAsync(Guid userId, Guid id, CancellationToken ct);
    Task DiscardPlanAsync(Guid userId, Guid id, CancellationToken ct);
    Task<ExecutionView?> GetExecutionAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task EnsureNotificationsAsync(Guid userId, CancellationToken ct);
    Task<TaskSuggestion> CaptureAsync(Guid userId, CaptureInput input, CancellationToken ct);
    Task<BreakdownView> BreakdownAsync(Guid userId, Guid taskId, bool useAi, CancellationToken ct);
    Task<AdminUserView[]> SearchUsersAsync(string? search, CancellationToken ct);
    Task SetUserActiveAsync(Guid adminId, Guid targetId, bool active, CancellationToken ct, string? reason = null);
    Task<AccountStatusLogView[]> GetAccountStatusLogsAsync(Guid targetId, CancellationToken ct);
    Task<JobRunView[]> GetJobRunsAsync(CancellationToken ct);
    Task<SystemStatusView> SystemStatusAsync(CancellationToken ct);
}
public interface ITaskInterpreter
{
    NoteAgentCapabilities Capabilities => new(false);
    Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string timeZoneId, DateTimeOffset now, CancellationToken ct);
    Task<TaskSuggestion> CaptureAsync(CaptureInput input, string timeZoneId, DateTimeOffset now, CancellationToken ct);
    Task<BreakdownView> BreakdownAsync(TaskInput task, bool useAi, CancellationToken ct);
}
