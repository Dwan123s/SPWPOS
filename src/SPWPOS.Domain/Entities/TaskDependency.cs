namespace SPWPOS.Domain.Entities;

public sealed class TaskDependency : OwnedRecord
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid TaskId { get; set; }
    public Guid PredecessorId { get; set; }
}
