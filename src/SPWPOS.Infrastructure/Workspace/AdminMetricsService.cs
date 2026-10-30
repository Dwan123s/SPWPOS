using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<AdminMetrics> GetAdminMetricsAsync(CancellationToken ct)
    {
        var today = new DateTimeOffset(Now.UtcDateTime.Date, TimeSpan.Zero);
        var usage = await db.AiUsageRecords.AsNoTracking().GroupBy(x => 1).Select(g => new {
            Input = g.Sum(x => x.InputTokens), Output = g.Sum(x => x.OutputTokens),
            HasInput = g.Any(x => x.InputTokens != null), HasOutput = g.Any(x => x.OutputTokens != null)
        }).SingleOrDefaultAsync(ct);
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return new(await db.Users.CountAsync(ct), await db.Users.CountAsync(x => x.LastLoginAt >= today, ct),
            await db.WorkSessions.CountAsync(x => x.Status == WorkSessionStatus.Completed, ct),
            usage?.HasInput == true ? usage.Input : null,
            usage?.HasOutput == true ? usage.Output : null,
            await db.SystemJobRuns.LongCountAsync(x => x.Status == "FAILED", ct), process.PrivateMemorySize64, process.TotalProcessorTime.TotalMilliseconds, Now);
    }
    public async Task<AdminHealth> GetAdminHealthAsync(CancellationToken ct)
    {
        var databaseHealthy = await db.Database.CanConnectAsync(ct);
        var emailConfigured = email is not null && !string.IsNullOrWhiteSpace(emailOptions?.Value.Host);
        if (!databaseHealthy) return new(false,
            new(emailConfigured, null, null, "Không đọc được lịch sử SMTP vì database chưa kết nối."),
            new(interpreter.Capabilities.AiAvailable, null, null, "Không đọc được lịch sử AI vì database chưa kết nối."), Now);
        var lastAi = await db.AiUsageRecords.AsNoTracking().Where(x => x.Status == "COMPLETED").OrderByDescending(x => x.FinishedAt)
            .Select(x => (long?)x.LatencyMilliseconds).FirstOrDefaultAsync(ct);
        var lastEmail = await db.InAppNotifications.AsNoTracking().Where(x => x.EmailSentAt != null).OrderByDescending(x => x.EmailSentAt)
            .Select(x => x.EmailSentAt).FirstOrDefaultAsync(ct);
        return new(databaseHealthy,
            new(emailConfigured, lastEmail is null ? null : true, null, lastEmail is null ? "Chưa ghi nhận lần gửi nhắc SMTP thành công; không chủ động gửi thư thử." : "Đã có lần gửi thành công; không phải phép đo kết nối hiện tại."),
            new(interpreter.Capabilities.AiAvailable, lastAi is null ? null : true, lastAi,
                "Độ trễ lần gọi thành công gần nhất; không gọi model tính phí để kiểm tra health."), Now);
    }
}
