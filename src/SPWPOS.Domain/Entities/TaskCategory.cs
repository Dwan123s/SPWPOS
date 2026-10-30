namespace SPWPOS.Domain.Entities;

public sealed class TaskCategory : OwnedRecord
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
}
