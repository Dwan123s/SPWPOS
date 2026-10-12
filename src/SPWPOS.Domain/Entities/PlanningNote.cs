namespace SPWPOS.Domain.Entities;

public sealed class PlanningNote : OwnedRecord
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
    public string AnalysisJson { get; set; } = "";
    public string CreatedTaskIdsJson { get; set; } = "[]";
    public Guid? PlanId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
