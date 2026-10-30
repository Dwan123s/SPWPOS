namespace SPWPOS.Domain.Entities;

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
