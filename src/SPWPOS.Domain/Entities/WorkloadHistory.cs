namespace SPWPOS.Domain.Entities;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class NotificationSetting
{
    public Guid UserId { get; set; }
    public bool InAppEnabled { get; set; } = true;
    public int DeadlineLeadHours { get; set; } = 24;
    public int SessionReminderLeadMinutes { get; set; } = 10;
    public string WorkloadWarningLevel { get; set; } = "HIGH";
}
public sealed class TaskTypeDefinition
{
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public sealed class AccountStatusLog : OwnedRecord
{
    public Guid AdminId { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class PlanItem : OwnedRecord
{
    public Guid PlanId { get; set; }
    public Guid TaskId { get; set; }
    public Guid? PreviousSessionId { get; set; }
    public string Action { get; set; } = "ADD";
    public DateTimeOffset? Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public int? PlannedMinutes { get; set; }
}
public sealed class PlanItemReason : OwnedRecord
{
    public Guid ItemId { get; set; }
    public string Code { get; set; } = "";
    public string? DetailJson { get; set; }
}
public sealed class StoredPlanWarning : OwnedRecord
{
    public Guid PlanId { get; set; }
    public Guid? TaskId { get; set; }
    public string Code { get; set; } = "";
    public string Detail { get; set; } = "";
}
public sealed class SessionExecution : OwnedRecord
{
    public Guid SessionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public long TimerSeconds { get; set; }
    public long ActualSeconds { get; set; }
    public bool IsUserAdjusted { get; set; }
}
public sealed class ExecutionSegment : OwnedRecord
{
    public Guid ExecutionId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
}
public sealed class EstimationObservation : OwnedRecord
{
    public Guid TaskId { get; set; }
    public string TaskType { get; set; } = "OTHER";
    public int EstimatedMinutes { get; set; }
    public double ActualMinutes { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
}
public sealed class PersonalCapacityProfile
{
    public Guid UserId { get; set; }
    public double GlobalEstimationBias { get; set; } = 1;
    public double CapacityFactor { get; set; } = .8;
    public int ObservationCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class TaskTypeBias
{
    public Guid UserId { get; set; }
    public string TaskType { get; set; } = "OTHER";
    public double BiasFactor { get; set; } = 1;
    public double AverageErrorRatio { get; set; }
    public int SampleCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class CapacitySnapshot : OwnedRecord
{
    public DateOnly WeekStart { get; set; }
    public int AvailableMinutes { get; set; }
    public int CompletedPlannedMinutes { get; set; }
    public double CapacityFactor { get; set; }
}
public sealed class RiskAssessment : OwnedRecord
{
    public Guid? TaskId { get; set; }
    public string Scope { get; set; } = "TASK";
    public string Level { get; set; } = "LOW";
    public double Score { get; set; }
    public double? LoadRatio { get; set; }
    public bool IsCurrent { get; set; } = true;
    public DateTimeOffset ComputedAt { get; set; }
}
public sealed class RiskReason : OwnedRecord
{
    public Guid AssessmentId { get; set; }
    public string Code { get; set; } = "";
    public string? DetailJson { get; set; }
}
public sealed class SystemJobRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string JobType { get; set; } = "";
    public string Status { get; set; } = "RUNNING";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
