using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;

namespace SPWPOS.Infrastructure.Commerce;

internal static class TeamRenewal
{
    public static async Task<TeamRenewalView?> GetAsync(ApplicationDbContext db, Guid teamId, DateTimeOffset now, CancellationToken ct)
    {
        var owner = await db.TeamProjects.AsNoTracking().Where(x => x.Id == teamId).Select(x => x.CreatorId).SingleAsync(ct);
        var count = await db.TeamMemberships.CountAsync(x => x.TeamId == teamId && x.Active, ct);
        var plan = await TeamPlans.ForAccountAsync(db, owner, now, ct);
        if (plan.MemberLimit is null || count <= plan.MemberLimit) return null;
        var purchases = await db.TeamPlanPurchases.AsNoTracking().Where(x => x.UserId == owner && x.PaidAt != null)
            .Select(x => new { x.PlanCode, x.PaidAt, x.ValidUntil }).ToArrayAsync(ct);
        var expired = purchases.Where(x => TeamPlans.Catalog.Any(p => p.Code == x.PlanCode && (p.MemberLimit is null || p.MemberLimit >= count)))
            .Select(x => (DateTimeOffset?)(x.ValidUntil ?? x.PaidAt!.Value.AddMonths(1))).Max();
        if (expired is null || expired > now) return null;
        var grace = expired.Value.AddDays(3);
        return new(now >= grace ? "Suspended" : "GracePeriod", expired.Value, grace);
    }
}
