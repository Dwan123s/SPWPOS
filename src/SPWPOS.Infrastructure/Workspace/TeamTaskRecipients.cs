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

    private void RequireOpenAvailabilityCheck(TeamTask task, Guid version)
    {
        Require(task.Version == version, "TASK_STALE", "Công việc đã thay đổi. Hãy tải lại và thử lại.", 409);
        Require(task.Status == "Open" && task.AssigneeId == null && task.Deadline > Now,
            "TASK_UNAVAILABLE", "Chỉ kiểm tra lịch cho công việc còn hạn và chưa được nhận.", 409);
    }

    public Task RequestTeamAvailabilityAsync(Guid uid, Guid taskId, TeamAvailabilityRequestInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await SharedTask(uid, taskId, ct);
        var leader = await TeamMember(uid, task.TeamId, ct, leader: true);
        RequireOpenAvailabilityCheck(task, input.Version);
        Require(task.DesignatedUserId == null || task.DesignatedUserId == input.UserId,
            "TASK_DESIGNATED", "Chỉ người được chỉ định mới có thể kiểm tra lịch cho công việc này.", 409);
        if (task.AvailabilityCheckVersion == task.Version && task.AvailabilityCheckUserId == input.UserId && task.AvailabilityCheckCanTake == null) return;
        await ValidateDesignatedMember(task.TeamId, input.UserId, ct);
        task.Version = Guid.NewGuid();
        task.AvailabilityCheckVersion = task.Version;
        task.AvailabilityCheckUserId = input.UserId;
        task.AvailabilityCheckCanTake = null;
        leader.MutationVersion = Guid.NewGuid();
        await AddNotification(input.UserId, "TEAM_AVAILABILITY_REQUESTED", "Trưởng nhóm muốn bạn kiểm tra lịch",
            $"Mở nhóm và kiểm tra lịch cho việc “{task.Name}”. Bạn có thể phản hồi trước khi nhận việc.",
            $"team-availability:{task.Id}:{task.Version}", null, ct);
    }, ct, false);

    public Task RespondTeamAvailabilityAsync(Guid uid, Guid taskId, TeamAvailabilityResponseInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await SharedTask(uid, taskId, ct);
        var member = await TeamMember(uid, task.TeamId, ct);
        RequireOpenAvailabilityCheck(task, input.Version);
        Require(task.AvailabilityCheckVersion == task.Version && task.AvailabilityCheckUserId == uid,
            "TEAM_AVAILABILITY_FORBIDDEN", "Yêu cầu kiểm tra lịch này không dành cho bạn.", 403);
        Require(task.AvailabilityCheckCanTake == null && input.CanTake.HasValue,
            "TEAM_AVAILABILITY_CLOSED", "Yêu cầu đã có phản hồi hoặc thiếu kết quả.", 409);
        task.Version = Guid.NewGuid();
        task.AvailabilityCheckVersion = task.Version;
        task.AvailabilityCheckCanTake = input.CanTake;
        member.MutationVersion = Guid.NewGuid();
        var response = input.CanTake == true ? "có thể nhận" : "chưa thể nhận";
        foreach (var leader in await db.TeamMemberships.Where(x => x.TeamId == task.TeamId && x.Active && x.Role == "Leader").Select(x => x.UserId).ToArrayAsync(ct))
            await AddNotification(leader, "TEAM_AVAILABILITY_RESPONDED", "Có phản hồi kiểm tra lịch",
                $"Thành viên phản hồi {response} việc “{task.Name}”. Mở nhóm để xem chi tiết.",
                $"team-availability:{task.Id}:{task.Version}", null, ct);
    }, ct, false);
}
