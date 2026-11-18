namespace SPWPOS.Application.Workspace;

public sealed record TeamPlan(string Code, int? MemberLimit, int AmountVnd)
{
    public static TeamPlan Free { get; } = new("free", 5, 0);
    public static TeamPlan Premium20 { get; } = new("premium20", 20, 50000);
    public static TeamPlan PremiumUnlimited { get; } = new("premiumUnlimited", null, 110000);
}

public sealed record TeamPlanCheckoutInput(string Code);
public sealed record TeamPlanOrderView(string PlanCode, string Status, string? CheckoutUrl);
public sealed record TeamPlanBillingView(bool Available, TeamPlan[] Plans, TeamPlanOrderView? Order, TeamPlan Plan, DateTimeOffset? ValidUntil = null);
