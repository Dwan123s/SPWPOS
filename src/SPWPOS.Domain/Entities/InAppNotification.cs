namespace SPWPOS.Domain.Entities;

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
