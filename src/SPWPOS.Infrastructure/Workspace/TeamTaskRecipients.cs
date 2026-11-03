using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task ValidateDesignatedMember(Guid teamId, Guid? recipient, CancellationToken ct)
    {
        if (recipient == null) return;
        var member = await db.TeamMemberships.SingleOrDefaultAsync(x => x.TeamId == teamId && x.UserId == recipient && x.Active, ct);
        Require(member != null && await db.Users.AnyAsync(x => x.Id == recipient && x.IsActive && x.EmailConfirmed, ct),
            "TEAM_RECIPIENT_UNAVAILABLE", "Người được chỉ định phải là thành viên đang hoạt động trong nhóm. Hãy tải lại danh sách thành viên.", 409);
        // Prevent a concurrent leave/removal from orphaning a newly designated task.
        member.MutationVersion = Guid.NewGuid();
    }
    private async Task NotifyDesignatedMember(TeamTask task, CancellationToken ct)
    {
        if (task.DesignatedUserId is not { } recipient) return;
        var team = await db.TeamProjects.Where(x => x.Id == task.TeamId).Select(x => x.Name).SingleAsync(ct);
        await AddNotification(recipient, "TEAM_TASK_DESIGNATED", "Bạn có công việc được giao đích danh",
            $"Nhóm {team}: “{task.Name}” được giao cho bạn. Mở nhóm để xem yêu cầu và nhận task; NORA sẽ xếp theo lịch riêng của bạn.",
            $"team-designated:{task.Id}:{task.Version}", null, ct);
    }
    public Task ChangeTeamTaskRecipientAsync(Guid uid, Guid taskId, TeamTaskRecipientChangeInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await SharedTask(uid, taskId, ct); var leader = await TeamMember(uid, task.TeamId, ct, true);
        Require(task.Status == "Open" && task.AssigneeId == null, "TASK_UNAVAILABLE", "Chỉ đổi người được chỉ định khi task chưa được nhận. Cần trả việc trước nếu đã nhận.", 409);
        Require(task.Version == input.Version, "TASK_STALE", "Task đã thay đổi. Hãy tải lại trước khi đổi người nhận.", 409);
        if (task.DesignatedUserId == input.UserId) return;
        await ValidateDesignatedMember(task.TeamId, input.UserId, ct);
        task.DesignatedUserId = input.UserId; task.Version = Guid.NewGuid(); leader.MutationVersion = Guid.NewGuid();
        TeamLog(uid, task.TeamId, task.Id, "TASK_RECIPIENT_CHANGED"); await NotifyDesignatedMember(task, ct);
    }, ct, false);
}
