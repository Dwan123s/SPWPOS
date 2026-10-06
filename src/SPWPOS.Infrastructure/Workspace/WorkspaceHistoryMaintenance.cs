using Microsoft.EntityFrameworkCore;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Workspace;

// Keep current assessments and business/security history. Only completed job
// diagnostics and superseded risk snapshots have a bounded retention period.
public sealed class WorkspaceHistoryMaintenance(ApplicationDbContext db, TimeProvider clock)
{
    public async Task PruneAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddDays(-30);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var risks = await db.RiskAssessments.Where(x => !x.IsCurrent && x.ComputedAt < cutoff).Take(1000).ToArrayAsync(ct);
        var ids = risks.Select(x => x.Id).ToArray();
        if (ids.Length > 0)
        {
            db.RiskReasons.RemoveRange(await db.RiskReasons.Where(x => ids.Contains(x.AssessmentId)).ToArrayAsync(ct));
            await db.SaveChangesAsync(ct);
            db.RiskAssessments.RemoveRange(risks);
        }
        db.SystemJobRuns.RemoveRange(await db.SystemJobRuns.Where(x => x.FinishedAt != null && x.FinishedAt < cutoff).Take(1000).ToArrayAsync(ct));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
