namespace SPWPOS.Domain.Entities;

public enum WorkSessionStatus { Scheduled, Running, Paused, Completed, Skipped, Missed, Cancelled, NeedsReview }
public sealed class WorkSession : OwnedRecord
{
    public Guid? SourceItemId { get; set; }
    public Guid? RepairSourceSessionId { get; set; }
    public string? SkipReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid TaskId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public WorkSessionStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public long ActualSeconds { get; set; }
    public bool ExperienceAwarded { get; set; }
    public DateTimeOffset? SegmentStartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string ReasonCode { get; set; } = "EARLIEST_FEASIBLE_SLOT";
}
