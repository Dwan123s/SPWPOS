using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;

namespace SPWPOS.Infrastructure.Commerce;

internal static class TeamPlans
{
    public static readonly TeamPlan[] Catalog = [TeamPlan.Free, TeamPlan.Premium20, TeamPlan.PremiumUnlimited];

    public static async Task<TeamPlan> CurrentAsync(ApplicationDbContext db, Guid teamId, DateTimeOffset now, CancellationToken ct)
    {
        var ownerId = await db.TeamProjects.Where(x => x.Id == teamId).Select(x => x.CreatorId).SingleAsync(ct);
        return await ForAccountAsync(db, ownerId, now, ct);
    }

    public static async Task<TeamPlan> ForAccountAsync(ApplicationDbContext db, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var paid = await db.TeamPlanPurchases.AsNoTracking()
            .Where(x => x.UserId == userId && x.PaidAt != null).Select(x => new { x.PlanCode, x.PaidAt, x.ValidUntil }).ToArrayAsync(ct);
        var paidCodes = paid.Where(x => (x.ValidUntil ?? x.PaidAt!.Value.AddMonths(1)) > now).Select(x => x.PlanCode).ToArray();
        return Catalog.LastOrDefault(plan => paidCodes.Contains(plan.Code)) ?? TeamPlan.Free;
    }

    public static async Task<DateTimeOffset?> ValidUntilAsync(ApplicationDbContext db, Guid userId, string code, CancellationToken ct)
    {
        var paid = await db.TeamPlanPurchases.AsNoTracking().Where(x => x.UserId == userId && x.PlanCode == code && x.PaidAt != null)
            .Select(x => new { x.PaidAt, x.ValidUntil }).ToArrayAsync(ct);
        return paid.Select(x => (DateTimeOffset?)(x.ValidUntil ?? x.PaidAt!.Value.AddMonths(1))).Max();
    }
}
