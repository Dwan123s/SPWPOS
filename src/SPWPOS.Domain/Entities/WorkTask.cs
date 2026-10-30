namespace SPWPOS.Domain.Entities;

public enum WorkTaskStatus { Todo, InProgress, Completed, Cancelled }
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
