namespace SPWPOS.Domain.Entities;

public enum WorkTaskStatus { Todo, InProgress, Completed, Cancelled }
public sealed class WorkTask : OwnedRecord, ITaskImportance
{
    public string ImportanceLevel { get; set; } = "Unknown";
    public string ImportanceSource { get; set; } = "Unknown";
    public string ImportanceReason { get; set; } = "";
    public double? ImportanceConfidence { get; set; }
    public DateTimeOffset? ImportanceConfirmedAt { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string? InheritedImportance { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string? InheritedImportanceReason { get; set; }
    public string EffectiveImportance => ImportanceLevel == "Unknown" ? InheritedImportance ?? "Unknown" : ImportanceLevel;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public int? ConfirmedRemainingMinutes { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public string UrgencyOverride { get; set; } = "Auto";
    public long MatrixOrder { get; set; }
    public string ChecklistJson { get; set; } = "[]";
    public bool TaskExperienceAwarded { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? CategoryId { get; set; }
    public Guid? ParentId { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public DateTimeOffset? WorkWindowStart { get; set; }
    public DateTimeOffset? WorkWindowEnd { get; set; }
    public int Priority { get; set; } = 2;
    public WorkTaskStatus Status { get; set; }
    public int Progress { get; set; }
    public int EstimatedMinutes { get; set; } = 60;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HasPlanningDetails => Deadline.HasValue && EstimatedMinutes > 0;
    public string TaskType { get; set; } = "OTHER";
    public bool IsHardDeadline { get; set; }
    public string CreationSource { get; set; } = "MANUAL";
    public int? AdjustedMinutes { get; set; }
    public double? AdjustmentFactor { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool Splittable { get; set; } = true;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool RequiresContinuousSession => ActivityContinuity.RequiresContinuousSession(Name, TaskType);
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool CanSplit => Splittable && !RequiresContinuousSession;
    public int MinimumSessionMinutes { get; set; } = 25;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
