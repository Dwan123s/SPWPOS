using System.Text.Json;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public Task<TeamBriefDraft> EditTeamBriefDraftAsync(Guid uid, Guid teamId, Guid proposalId, TeamDraftEditInput input, CancellationToken ct) => Mutate(uid, async () => {
        await TeamMember(uid, teamId, ct, true);
        var row = await Find<SPWPOS.Domain.Entities.PrivatePlanningProposal>(uid, proposalId, ct);
        var payload = row.Kind == "TEAM_BRIEF" ? JsonSerializer.Deserialize<TeamBriefPayload>(row.PayloadJson) : null;
        Require(payload != null && payload.TeamId == teamId, "NOT_FOUND", "Không tìm thấy bản chia việc.", 404);
        Require(!row.Applied && row.ExpiresAt > Now, "PROPOSAL_EXPIRED", "Bản nháp đã được giao hoặc hết hạn. Hãy tải lại nhóm.", 409);
        Require(row.Revision == input.Revision, "DRAFT_STALE", "Bản nháp vừa được sửa ở nơi khác. Hãy tải lại bản mới nhất.", 409);
        Require(payload.Members.SequenceEqual(await BriefMembers(teamId, ct)), "TEAM_MEMBERS_CHANGED", "Thành viên nhóm đã thay đổi. Hãy chia việc lại.", 409);
        var original = payload.PendingTasks is { Length: > 0 } pending ? pending : payload.Tasks.Select(t => new TeamBriefPendingTask(t.Id,t.Name,t.Description,t.EstimatedMinutes,t.Deadline,false,t.Importance)).ToArray();
        Require(input.Tasks is { Length: > 0 and <= 20 } && input.Tasks.Length == original.Length && input.Tasks.All(t=>t != null) &&
            input.Tasks.Select(t=>t.Id).Distinct().Count() == original.Length && input.Tasks.All(t=>original.Any(o=>o.Id==t.Id)),
            "INVALID_DRAFT_TASKS", "Chỉ chỉnh sửa các gói việc trong bản nháp hiện tại; không thêm, bỏ hoặc thay mã task.");
        Require(input.Tasks.All(t => !string.IsNullOrWhiteSpace(t.Name) && t.Name.Length <= 200 && !string.IsNullOrWhiteSpace(t.Description) && t.Description.Length <= 4000 &&
            t.EstimatedMinutes is >= 1 and <= 43200 && (t.Deadline == null || t.Deadline > Now && t.Deadline <= Now.AddDays(365))),
            "VALIDATION_ERROR", "Tên, nội dung, thời lượng hoặc hạn chót chưa hợp lệ. Hạn chót cần ở tương lai.");
        Require(input.Tasks.Select(t=>t.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()==input.Tasks.Length,
            "VALIDATION_ERROR", "Mỗi gói việc cần có tên khác nhau.");
        var edited = original.Select(o => {
            var edit = input.Tasks.Single(t=>t.Id==o.Id);
            var importance = o.Importance?.Source == "AI" && (o.Name != edit.Name.Trim() || o.Description != edit.Description.Trim())
                ? new ImportanceView("Unknown","Unknown","Nội dung đã được trưởng nhóm chỉnh sửa; cần đánh giá lại mức quan trọng.",null) : o.Importance;
            return new TeamBriefPendingTask(o.Id,edit.Name.Trim(),edit.Description.Trim(),edit.EstimatedMinutes,edit.Deadline,edit.Deadline==null,importance);
        }).ToArray();
        var ready = edited.All(t=>t.Deadline != null);
        var tasks = ready ? edited.Select(t => {
            var old = payload.Tasks.SingleOrDefault(o=>o.Id==t.Id);
            return old != null ? old with {Name=t.Name,Description=t.Description,EstimatedMinutes=t.EstimatedMinutes,Deadline=t.Deadline!.Value,Importance=t.Importance}
                : new SharedTaskView(t.Id,t.Name,t.Description,t.EstimatedMinutes,t.Deadline!.Value,2,true,null,0,"Open",Guid.NewGuid(),false,Importance:t.Importance);
        }).ToArray() : [];
        var question = ready ? "" : "Hãy chốt hạn chót cho các gói còn thiếu, hoặc áp dụng một hạn chót chung cho cả nhóm.";
        var warnings = new[] { "Đây là bản nháp đã được trưởng nhóm chỉnh sửa trực tiếp. Chỉ giao việc khi bạn xác nhận; lịch cá nhân chưa thay đổi." };
        var updated = payload with {Tasks=tasks,PendingTasks=ready ? null : edited,Question=question,Warnings=warnings};
        row.PayloadJson = JsonSerializer.Serialize(updated); row.Revision = Guid.NewGuid();
        return new TeamBriefDraft(row.Id,payload.Summary,question,payload.SplitReason,payload.Members.Length,payload.SourceFile?.Name,tasks,warnings,ready ? null : edited,row.Revision);
    }, ct, false, System.Data.IsolationLevel.Serializable);
}
