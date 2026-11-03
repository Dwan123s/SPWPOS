namespace SPWPOS.Application.Workspace;

public sealed record RevenueTotal(long AmountVnd, int PaidOrders);
public sealed record RevenuePoint(DateOnly From, DateOnly To, long AmountVnd, int PaidOrders);
public sealed record RevenueReport(DateOnly From, DateOnly To, string GroupBy, string TimeZone, DateTimeOffset GeneratedAt,
    RevenueTotal Today, RevenueTotal Week, RevenueTotal Month, RevenueTotal AllTime,
    RevenueTotal Selected, int PayingCustomers, RevenuePoint[] Points);
public sealed record AdminPurchase(Guid Id, long OrderCode, Guid UserId, string Email, string BuyerName,
    string SkinCode, string ProductName, int AmountVnd, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? PaidAt, string? ReceiptReference);
public sealed record PurchaseHistory(DateOnly From, DateOnly To, int Page, int PageSize, int Total, AdminPurchase[] Items);
