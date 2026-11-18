namespace SPWPOS.Domain.Entities;

public sealed class TeamPlanPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TeamId { get; set; }
    public Guid UserId { get; set; }
    public long OrderCode { get; set; }
    public string PlanCode { get; set; } = "";
    public int AmountVnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? ClosedStatus { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public string? ReceiptReference { get; set; }
}
