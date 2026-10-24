namespace SPWPOS.Domain.Entities;

public enum WorkTaskStatus { Todo, InProgress, Completed, Cancelled }
public enum WorkSessionStatus { Scheduled, Running, Paused, Completed, Skipped, Missed, Cancelled }
public abstract class OwnedRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
}
public sealed class TaskCategory : OwnedRecord
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
}
public sealed class WorkTask : OwnedRecord
{
    public Guid Version { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? CategoryId { get; set; }
    public Guid? ParentId { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset? WorkWindowStart { get; set; }
    public DateTimeOffset? WorkWindowEnd { get; set; }
    public int Priority { get; set; } = 2;
    public WorkTaskStatus Status { get; set; }
    public int Progress { get; set; }
    public int EstimatedMinutes { get; set; } = 60;
    public string TaskType { get; set; } = "OTHER";
    public bool IsHardDeadline { get; set; }
    public string CreationSource { get; set; } = "MANUAL";
    public int? AdjustedMinutes { get; set; }
    public double? AdjustmentFactor { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool Splittable { get; set; } = true;
    public int MinimumSessionMinutes { get; set; } = 25;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
public sealed class TaskDependency : OwnedRecord
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid TaskId { get; set; }
    public Guid PredecessorId { get; set; }
}
public sealed class WeeklyAvailability : OwnedRecord
{
    public int DayOfWeek { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
}
public sealed class AvailabilityException : OwnedRecord
{
    public bool OverridesWeeklyAvailability { get; set; }
    public DateOnly Date { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public bool IsAvailable { get; set; }
    public string Note { get; set; } = "";
}
public sealed class FixedEvent : OwnedRecord
{
    public bool? IsUrgent { get; set; }
    public string Description { get; set; } = "";
    public string EventType { get; set; } = "OTHER";
    public string Name { get; set; } = "";
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
}
public sealed class WorkPreference : OwnedRecord
{
    public int MaximumDailyMinutes { get; set; } = 480;
    public bool AutoReschedule { get; set; } = true;
    public int PlanningHorizonDays { get; set; } = 14;
    public string PreferredPeriod { get; set; } = "NONE";
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public int MinimumSessionMinutes { get; set; } = 25;
    public int MaximumSessionMinutes { get; set; } = 90;
    public int BreakMinutes { get; set; } = 10;
    public int PreferredStartMinute { get; set; } = 8 * 60;
    public int PreferredEndMinute { get; set; } = 18 * 60;
    public Guid Revision { get; set; } = Guid.NewGuid();
}
public sealed class PlanProposal : OwnedRecord
{
    public Guid? PreviousPlanId { get; set; }
    public string Kind { get; set; } = "INITIAL";
    public string TriggerEvent { get; set; } = "MANUAL";
    public string Status { get; set; } = "PROPOSED";
    public DateTimeOffset HorizonStart { get; set; }
    public DateTimeOffset HorizonEnd { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid Revision { get; set; }
    public string PayloadJson { get; set; } = "";
    public bool IsApplied { get; set; }
}
public sealed class WorkSession : OwnedRecord
{
    public Guid? SourceItemId { get; set; }
    public string? SkipReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid TaskId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public WorkSessionStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public long ActualSeconds { get; set; }
    public DateTimeOffset? SegmentStartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string ReasonCode { get; set; } = "EARLIEST_FEASIBLE_SLOT";
}
public sealed class InAppNotification : OwnedRecord
{
    public bool IsVisible { get; set; } = true;
    public bool EmailPending { get; set; }
    public DateTimeOffset? EmailSentAt { get; set; }
    public int EmailAttempts { get; set; }
    public DateTimeOffset? NextEmailAttemptAt { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? PlanId { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string DedupeKey { get; set; } = "";
    public Guid? TaskId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
