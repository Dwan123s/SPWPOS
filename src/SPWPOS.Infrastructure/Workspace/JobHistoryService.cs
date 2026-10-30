using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    // The job record is committed outside the business transaction so failures survive rollback.
    private async Task<T> RunJob<T>(Guid uid, string kind, Func<Task<T>> action, CancellationToken ct, Func<T, bool>? succeeded = null)
    {
        var job = new SystemJobRun { UserId = uid, JobType = kind, StartedAt = Now };
        db.SystemJobRuns.Add(job); await db.SaveChangesAsync(ct);
        try
        {
            var result = kind == "AI_REQUEST" ? await aiUsage.RunAsync(uid,kind,action,ct) : await action(); job.Status = succeeded is null || succeeded(result) ? "SUCCESS" : "FAILED"; job.FinishedAt = Now;
            if (job.Status == "FAILED") job.ErrorMessage = "AI_UNAVAILABLE_OR_INVALID_RESPONSE";
            await db.SaveChangesAsync(ct); return result;
        }
        catch (Exception e)
        {
            db.ChangeTracker.Clear();
            var saved = await db.SystemJobRuns.SingleAsync(x => x.Id == job.Id, CancellationToken.None);
            saved.Status = "FAILED"; saved.FinishedAt = Now;
            saved.ErrorMessage = e is SPWPOS.Application.Authentication.AuthException failure ? failure.Code : e.GetType().Name;
            await db.SaveChangesAsync(CancellationToken.None); throw;
        }
    }
    public Task<JobRunView[]> GetJobRunsAsync(CancellationToken ct) => db.SystemJobRuns.AsNoTracking().OrderByDescending(x => x.StartedAt).Take(100)
        .Select(x => new JobRunView(x.Id, null, x.JobType, x.Status, x.ErrorMessage, x.StartedAt, x.FinishedAt)).ToArrayAsync(ct);
    public Task<AccountStatusLogView[]> GetAccountStatusLogsAsync(Guid uid, CancellationToken ct) => Mine<AccountStatusLog>(uid).AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100)
        .Select(x => new AccountStatusLogView(x.Id, x.UserId, x.AdminId, x.Action, x.Reason, x.CreatedAt)).ToArrayAsync(ct);
}
