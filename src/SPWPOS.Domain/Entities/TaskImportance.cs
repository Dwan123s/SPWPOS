namespace SPWPOS.Domain.Entities;

public interface ITaskImportance
{
    string ImportanceLevel { get; set; }
    string ImportanceSource { get; set; }
    string ImportanceReason { get; set; }
    double? ImportanceConfidence { get; set; }
    DateTimeOffset? ImportanceConfirmedAt { get; set; }
}

public sealed class PriorityAlertState : OwnedRecord
{
    public Guid TaskId { get; set; }
    public string LastSignal { get; set; } = "";
    public Guid Episode { get; set; } = Guid.NewGuid();
    public DateTimeOffset? ClearSince { get; set; }
}
