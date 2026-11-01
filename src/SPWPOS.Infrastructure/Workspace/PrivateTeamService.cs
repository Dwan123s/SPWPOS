using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record TeamDraftPayload(Guid TeamId, Guid MembershipVersion, SharedTaskView Task);
    private sealed record ClaimPayload(WorkTask Task, PlanView[] Plans, WorkSession[] Before, bool IsChange = false, string Analysis = "");
    private async Task<TeamMembership> TeamMember(Guid uid, Guid teamId, CancellationToken ct, bool leader = false)
    {
        var member = await db.TeamMemberships.SingleOrDefaultAsync(x => x.TeamId == teamId && x.UserId == uid && x.Active, ct);
        Require(member is not null && (!leader || member.Role == "Leader"), "TEAM_FORBIDDEN", "Bạn không có quyền thực hiện thao tác trong nhóm này.", 403);
        return member;
    }
    private async Task<TeamTask> SharedTask(Guid uid, Guid taskId, CancellationToken ct)
    {
        var task = await db.TeamTasks.SingleOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new AuthException("NOT_FOUND", "Không tìm thấy công việc nhóm.", 404);
        await TeamMember(uid, task.TeamId, ct); return task;
    }
    private static SharedTaskView SharedDto(TeamTask t) => new(t.Id, t.Name, t.Description, t.EstimatedMinutes, t.Deadline, t.Priority, t.Splittable, t.AssigneeId, t.Progress, t.Status, t.Version, t.PendingChangeJson != null, t.CompletionRequestId, t.CompletionSubmittedAt, t.CompletionReviewedAt, t.CompletionReviewedBy, t.CompletionApproved, t.CompletionReviewNote);
    private void TeamLog(Guid uid, Guid teamId, Guid? taskId, string kind) => db.TeamActivities.Add(new() { ActorId = uid, TeamId = teamId, TaskId = taskId, Kind = kind, CreatedAt = Now });
    // DTOs for group reads are deliberately projected from shared tables only.
    public async Task<TeamView[]> ListTeamsAsync(Guid uid, CancellationToken ct) => await
        (from m in db.TeamMemberships.AsNoTracking() join t in db.TeamProjects.AsNoTracking() on m.TeamId equals t.Id
         where m.UserId == uid && m.Active orderby t.Name select new TeamView(t.Id, t.Name, m.Role)).ToArrayAsync(ct);
    public Task<Guid> CreateTeamAsync(Guid uid, TeamNameInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var name = input.Name.Trim(); Require(name.Length is > 0 and <= 120, "VALIDATION_ERROR", "Tên nhóm cần có 1–120 ký tự.");
        var team = new TeamProject { Name = name, CreatorId = uid, CreatedAt = Now }; db.TeamProjects.Add(team);
        db.TeamMemberships.Add(new() { TeamId = team.Id, UserId = uid, Role = "Leader" }); TeamLog(uid, team.Id, null, "TEAM_CREATED");
        await Task.CompletedTask; return team.Id;
    }, ct, false);
    public async Task<TeamDetailView> GetTeamAsync(Guid uid, Guid teamId, CancellationToken ct)
    {
        var member = await TeamMember(uid, teamId, ct); var team = await db.TeamProjects.AsNoTracking().SingleAsync(x => x.Id == teamId, ct);
        var members = await (from m in db.TeamMemberships.AsNoTracking() join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            join p in db.UserProfiles.AsNoTracking() on u.Id equals p.UserId into profiles
            from p in profiles.DefaultIfEmpty()
            where m.TeamId == teamId && m.Active select new TeamMemberView(m.UserId,
                p != null && p.DisplayName != "" ? p.DisplayName : u.FirstName + " " + u.LastName, m.Role, m.Version,
                p != null ? p.AvatarUrl : "")).ToArrayAsync(ct);
        var tasks = await db.TeamTasks.AsNoTracking().Where(x => x.TeamId == teamId).OrderBy(x => x.Deadline).Take(500).ToArrayAsync(ct);
        var activity = await db.TeamActivities.AsNoTracking().Where(x => x.TeamId == teamId).OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new TeamActivityView(x.ActorId, x.TaskId, x.Kind, x.CreatedAt)).ToArrayAsync(ct);
        var visibleRequests = tasks.Where(x => x.CompletionRequestId != null && (member.Role == "Leader" || x.AssigneeId == uid))
            .Select(x => x.CompletionRequestId!.Value).ToArray();
        var submissions = await db.TeamCompletionSubmissions.AsNoTracking().Where(x => visibleRequests.Contains(x.Id))
            .Select(x => new { x.Id, x.Note }).ToDictionaryAsync(x => x.Id, ct);
        var reports = await db.TeamCompletionFiles.AsNoTracking().Where(x => visibleRequests.Contains(x.SubmissionId))
            .Select(x => new { x.SubmissionId, File = new TeamCompletionFileView(x.Id, x.Name, x.MimeType, x.Size, x.CreatedAt) }).ToArrayAsync(ct);
        var views = tasks.Select(t => {
            var dto = SharedDto(t);
            return t.CompletionRequestId is { } request && visibleRequests.Contains(request)
                ? dto with { CompletionSubmissionNote = submissions.GetValueOrDefault(request)?.Note,
                    CompletionFiles = reports.Where(x => x.SubmissionId == request).Select(x => x.File).ToArray() } : dto;
        }).ToArray();
        return new(new(teamId, team.Name, member.Role), members, views, activity);
    }
    private static string InviteHash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));
    private static string InviteCode(string code)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        Require(normalized.Length == 40 && normalized.All(Uri.IsHexDigit), "INVITE_INVALID", "Mã hoặc link mời không hợp lệ.");
        return normalized;
    }
    public async Task<TeamInvitePreview> PreviewTeamInviteAsync(Guid uid, string code, CancellationToken ct)
    {
        Require(await db.Users.AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct), "FORBIDDEN", "Hãy đăng nhập bằng tài khoản đã xác minh.", 403);
        var hash = InviteHash(InviteCode(code));
        var invite = await db.TeamInvites.AsNoTracking().SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
        Require(invite != null && !invite.Revoked && invite.ExpiresAt > Now && invite.RemainingUses > 0, "INVITE_INVALID", "Link mời đã hết hạn, bị thu hồi hoặc hết lượt sử dụng. Hãy xin trưởng nhóm link mới.", 409);
        var name = await db.TeamProjects.Where(x => x.Id == invite.TeamId).Select(x => x.Name).SingleAsync(ct);
        var count = await db.TeamMemberships.CountAsync(x => x.TeamId == invite.TeamId && x.Active, ct);
        var joined = await db.TeamMemberships.AnyAsync(x => x.TeamId == invite.TeamId && x.UserId == uid && x.Active, ct);
        return new(name, count, invite.ExpiresAt, joined);
    }
    public Task<TeamInviteView> CreateTeamInviteAsync(Guid uid, Guid teamId, CancellationToken ct) => Mutate(uid, async () =>
    {
        await TeamMember(uid, teamId, ct, true); var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var invite = new TeamInvite { TeamId = teamId, CodeHash = InviteHash(code), ExpiresAt = Now.AddDays(7) }; db.TeamInvites.Add(invite);
        return new TeamInviteView(invite.Id, code, invite.ExpiresAt);
    }, ct, false);
    public Task RevokeTeamInviteAsync(Guid uid, Guid teamId, Guid inviteId, CancellationToken ct) => Mutate(uid, async () =>
    {
        await TeamMember(uid, teamId, ct, true); var invite = await db.TeamInvites.SingleOrDefaultAsync(x => x.TeamId == teamId && x.Id == inviteId, ct);
        Require(invite is not null, "NOT_FOUND", "Mã mời không tồn tại.", 404); invite.Revoked = true; invite.Version = Guid.NewGuid();
    }, ct, false);
    public Task<Guid> JoinTeamAsync(Guid uid, string code, CancellationToken ct) => Mutate(uid, async () =>
    {
        var hash = InviteHash(InviteCode(code));
        var invite = await db.TeamInvites.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
        Require(invite is not null && !invite.Revoked && invite.ExpiresAt > Now && invite.RemainingUses > 0, "INVITE_INVALID", "Mã mời đã hết hạn hoặc bị thu hồi.", 409);
        var member = await db.TeamMemberships.SingleOrDefaultAsync(x => x.UserId == uid && x.TeamId == invite.TeamId, ct);
        if (member?.Active == true) return invite.TeamId;
        if (member is null) db.TeamMemberships.Add(new() { UserId = uid, TeamId = invite.TeamId });
        else { member.Active = true; member.Role = "Member"; member.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid(); }
        invite.RemainingUses--; invite.Version = Guid.NewGuid(); TeamLog(uid, invite.TeamId, null, "MEMBER_JOINED"); return invite.TeamId;
    }, ct, false);
    public Task RemoveTeamMemberAsync(Guid uid, Guid teamId, Guid memberId, Guid version, CancellationToken ct) => Mutate(uid, async () =>
    {
        await TeamMember(uid, teamId, ct, uid != memberId); var target = await TeamMember(memberId, teamId, ct);
        Require(target.Version == version, "MEMBER_STALE", "Thành viên đã thay đổi. Tải lại nhóm.", 409);
        Require(target.Role != "Leader", "LEADER_REQUIRED", "Trưởng nhóm cần tiếp tục quản lý nhóm.", 409);
        Require(!await db.TeamTasks.AnyAsync(x => x.TeamId == teamId && x.AssigneeId == memberId && (x.Status == "Assigned" || x.Status == "PendingReview"), ct), "ASSIGNMENTS_ACTIVE", "Cần trả hoặc hoàn thành các việc đang nhận trước khi rời nhóm.", 409);
        target.Active = false; target.Version = Guid.NewGuid(); TeamLog(uid, teamId, null, "MEMBER_LEFT");
    }, ct, false);
    private async Task<SharedTaskView?> InterpretSharedDraft(Guid uid, Guid teamId, string text, SharedTaskView? current, CancellationToken ct)
    {
        Require(interpreter is IPlanningAgentInterpreter && GetAgentCapabilities().AiAvailable, "AI_NOT_CONFIGURED", "AI chưa khả dụng. Vui lòng thử lại sau.", 503);
        var preference = await db.WorkPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid, ct);
        var context = JsonSerializer.Serialize(new { mode = "TEAM_TASK_DRAFT", current, instructions = "Chỉ tạo bản nháp một task dùng chung từ lời người dùng. Không đọc hoặc yêu cầu lịch cá nhân. Trả tasks với name, deadline, priority, splittable. Nếu thiếu thời lượng, đề xuất estimatedMinutes trong tasks và giải thích đây là ước lượng cần xác nhận. Không tự suy ra deadline; hỏi khi thiếu. Không thực thi." });
        var intent = await RunJob(uid, "AI_REQUEST", () => ((IPlanningAgentInterpreter)interpreter).InterpretAsync(text, context, [], preference?.TimeZoneId ?? "Asia/Ho_Chi_Minh", Now, ct), ct);
        var task = intent.Tasks.Length == 1 ? intent.Tasks[0] : null;
        if (task is null) return null;
        var minutes = task.EstimatedMinutes ?? ProposedDuration(intent.Question + " " + intent.Reply) ?? current?.EstimatedMinutes;
        var deadline = task.Deadline ?? current?.Deadline;
        if (task.Deadline is { } proposedDeadline && proposedDeadline != current?.Deadline &&
            (string.IsNullOrWhiteSpace(task.DeadlineEvidence) || !text.Contains(task.DeadlineEvidence, StringComparison.OrdinalIgnoreCase)))
            return null;
        if (minutes is not (>= 1 and <= 43200) || deadline is null || deadline <= Now || string.IsNullOrWhiteSpace(task.Name)) return null;
        Require(task.Name.Length <= 200 && deadline <= Now.AddDays(365), "INVALID_TEAM_TASK", "Task cần tên ngắn và deadline trong một năm tới.");
        return new(current?.Id ?? Guid.NewGuid(), task.Name.Trim(), current?.Description ?? text, minutes.Value, deadline.Value, Math.Clamp(task.Priority ?? current?.Priority ?? 2, 1, 4), task.Splittable ?? current?.Splittable ?? true, current?.AssigneeId, current?.Progress ?? 0, current?.Status ?? "Open", current?.Version ?? Guid.NewGuid(), false);
    }
    public async Task<TeamTaskDraft> DraftTeamTaskAsync(Guid uid, Guid teamId, TeamTextInput input, CancellationToken ct)
    {
        var member = await TeamMember(uid, teamId, ct, true);
        var task = await InterpretSharedDraft(uid, teamId, input.Text, null, ct);
        await db.Entry(member).ReloadAsync(ct);
        member = await TeamMember(uid, teamId, ct, true);
        if (task is null) return new(Guid.Empty, "Chưa lưu công việc.", "Hãy nêu tên việc và hạn chót; NORA sẽ đề xuất thời lượng để bạn xác nhận.", null);
        var row = new PrivatePlanningProposal { UserId = uid, Kind = "TEAM_DRAFT", ExpiresAt = Now.AddMinutes(20), PayloadJson = JsonSerializer.Serialize(new TeamDraftPayload(teamId, member.Version, task)) };
        db.PrivatePlanningProposals.Add(row); await db.SaveChangesAsync(ct);
        return new(row.Id, "Bản nháp và thời lượng đề xuất. Chỉ tạo task chung sau khi bạn xác nhận.", "", task);
    }
    public Task<Guid> ConfirmTeamTaskDraftAsync(Guid uid, Guid teamId, Guid proposalId, CancellationToken ct) => Mutate(uid, async () =>
    {
        var member = await TeamMember(uid, teamId, ct, true); var row = await Find<PrivatePlanningProposal>(uid, proposalId, ct);
        var payload = JsonSerializer.Deserialize<TeamDraftPayload>(row.PayloadJson);
        Require(row.Kind is "TEAM_DRAFT" or "TEAM_CHANGE" && payload is not null && payload.TeamId == teamId, "NOT_FOUND", "Không tìm thấy bản nháp.", 404);
        if (row.Applied) return payload.Task.Id;
        Require(row.ExpiresAt > Now && payload.MembershipVersion == member.Version && payload.Task.Deadline > Now, "PROPOSAL_EXPIRED", "Bản nháp hết hạn hoặc quyền đã thay đổi.", 409);
        var t = payload.Task;
        if (row.Kind == "TEAM_CHANGE")
        {
            await TeamMember(uid, teamId, ct, true);
            var existing = await SharedTask(uid, t.Id, ct);
            Require(existing.Version == t.Version && existing.Status is not ("Completed" or "PendingReview"), "TASK_STALE", "Task đã thay đổi, hãy tạo lại đề xuất.", 409);
            if (existing.AssigneeId != null) existing.PendingChangeJson = JsonSerializer.Serialize(t);
            else { existing.Name = t.Name; existing.Description = t.Description; existing.EstimatedMinutes = t.EstimatedMinutes; existing.Deadline = t.Deadline; existing.Priority = t.Priority; existing.Splittable = t.Splittable; }
            existing.Version = Guid.NewGuid(); TeamLog(uid, teamId, t.Id, "TASK_CHANGE_PROPOSED");
        }
        else db.TeamTasks.Add(new() { Id = t.Id, TeamId = teamId, Name = t.Name, Description = t.Description, EstimatedMinutes = t.EstimatedMinutes, Deadline = t.Deadline, Priority = t.Priority, Splittable = t.Splittable });
        member.MutationVersion = Guid.NewGuid(); row.Applied = true; TeamLog(uid, teamId, t.Id, "TASK_CREATED"); return t.Id;
    }, ct, false);
}
