using Microsoft.EntityFrameworkCore;
using SPWPOS.Infrastructure.Commerce;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task EnsureTeamRenewalNotifications(Guid uid, CancellationToken ct)
    {
        var teams = await (from member in db.TeamMemberships.AsNoTracking()
            join team in db.TeamProjects.AsNoTracking() on member.TeamId equals team.Id
            where member.UserId == uid && member.Active
            select new { team.Id, team.Name }).ToArrayAsync(ct);
        foreach (var team in teams)
        {
            var renewal = await TeamRenewal.GetAsync(db, team.Id, Now, ct);
            if (renewal is null) continue;
            var suspended = renewal.Status == "Suspended";
            var day = Math.Min(3, Math.Max(0, (int)(Now - renewal.ExpiredAt).TotalDays));
            var deadline = TimeZoneInfo.ConvertTime(renewal.GraceUntil, PlanningEngine.Zone("Asia/Ho_Chi_Minh")).ToString("dd/MM/yyyy HH:mm");
            await AddNotification(uid, suspended ? "TEAM_PREMIUM_SUSPENDED" : "TEAM_PREMIUM_EXPIRED",
                suspended ? "Nhóm đã bị vô hiệu hóa" : "Nhóm cần gia hạn Premium trong 3 ngày",
                suspended ? $"Nhóm {team.Name} đã quá hạn gia hạn 3 ngày. Chủ nhóm cần gia hạn Premium để mở lại nhóm."
                    : $"Premium của nhóm {team.Name} đã hết hạn. Chủ nhóm cần gia hạn trước {deadline} (giờ Việt Nam) để tiếp tục sử dụng nhóm.",
                $"team-premium:{team.Id}:{renewal.ExpiredAt.UtcTicks}:{day}", null, ct);
        }
    }
}
