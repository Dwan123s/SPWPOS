namespace SPWPOS.Domain.Entities;

public sealed class SkinPurchase : OwnedRecord
{
    public Guid RequestId { get; set; }
    public long OrderCode { get; set; }
    public string SkinCode { get; set; } = "fox";
    public int AmountVnd { get; set; } = 10000;
    public string Status { get; set; } = "CREATING";
    public string? PaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? AccountNumber { get; set; }
    public string? ReceiptReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class OwnedPetSkin : OwnedRecord
{
    public string SkinCode { get; set; } = "";
    public Guid PurchaseId { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
}
