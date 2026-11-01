using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record BriefMember(Guid UserId, Guid Version, string Role);
    private sealed record TeamBriefPayload(Guid TeamId, BriefMember[] Members, SharedTaskView[] Tasks);
    private Task<BriefMember[]> BriefMembers(Guid teamId, CancellationToken ct) => db.TeamMemberships.AsNoTracking()
        .Where(x => x.TeamId == teamId && x.Active).OrderBy(x => x.UserId).Select(x => new BriefMember(x.UserId, x.Version, x.Role)).ToArrayAsync(ct);

    public async Task<TeamBriefDraft> DraftTeamBriefAsync(Guid uid, Guid teamId, TeamBriefInput input, CancellationToken ct)
    {
        await TeamMember(uid, teamId, ct, true);
        Require(interpreter is ITeamBriefInterpreter && GetAgentCapabilities().AiAvailable, "AI_NOT_CONFIGURED", "NORA chưa hỗ trợ đọc tài liệu trên máy chủ này.", 503);
        Require(input.Text is not null && input.Text.Length <= 12000 && (input.File != null || !string.IsNullOrWhiteSpace(input.Text)),
            "VALIDATION_ERROR", "Gửi tài liệu hoặc mô tả công việc, tối đa 12.000 ký tự.");
        var members = await BriefMembers(teamId, ct);
        Require(members.Length is >= 1 and <= 20, "TEAM_BRIEF_MEMBER_LIMIT", "Mỗi lần phân tích hỗ trợ nhóm tối đa 20 thành viên. Hãy chia dự án thành các nhóm nhỏ hơn.");
        var document = input.File == null ? null : TeamBriefReader.Read(input.File);
        var zone = await db.WorkPreferences.AsNoTracking().Where(x => x.UserId == uid).Select(x => x.TimeZoneId).SingleOrDefaultAsync(ct) ?? "Asia/Ho_Chi_Minh";
        var result = await RunJob(uid, "AI_REQUEST", () => ((ITeamBriefInterpreter)interpreter).AnalyzeTeamBriefAsync(
            new(members.Length, zone, Now, document?.Name, input.Text, document?.Text, document?.PdfData), ct), ct);
        // No calendars, profile details or member names were sent to the model.
        db.ChangeTracker.Clear();
        await TeamMember(uid, teamId, ct, true);
        Require(members.SequenceEqual(await BriefMembers(teamId, ct)), "TEAM_MEMBERS_CHANGED", "Thành viên nhóm đã thay đổi trong lúc phân tích. Hãy để NORA chia việc lại.", 409);
        Require(result.Tasks != null && result.Tasks.Length <= 20 && result.Tasks.All(x => x != null) && result.Warnings is { Length: <= 20 } &&
            result.Warnings.All(x => x != null && x.Length <= 1000) && result.Summary?.Length <= 2000 &&
            result.Question?.Length <= 2000 && result.SplitReason?.Length <= 2000, "TEAM_BRIEF_INVALID", "NORA chưa trả bản phân tích hợp lệ.", 503);
        if (!string.IsNullOrWhiteSpace(result.Question) || result.Tasks.Length == 0)
            return new(Guid.Empty, result.Summary, string.IsNullOrWhiteSpace(result.Question) ? "Chưa đủ thông tin. Hãy bổ sung phạm vi công việc và deadline." : result.Question,
                result.SplitReason, members.Length, document?.Name, [], result.Warnings);
        Require(result.Tasks.Length == members.Length && result.Tasks.Select(x => x.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == result.Tasks.Length,
            "TEAM_BRIEF_SPLIT_INVALID", "NORA chưa chia đủ các gói việc khác nhau theo số thành viên hiện tại. Hãy yêu cầu phân tích lại.", 503);
        var source = input.Text + "\n" + document?.Text;
        var tasks = new List<SharedTaskView>();
        foreach (var task in result.Tasks)
        {
            static bool ValidItems(string[]? items, int count, int length) => items != null && items.Length <= count &&
                items.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= length);
            Require(!string.IsNullOrWhiteSpace(task.Name) && task.Name.Length <= 200 && !string.IsNullOrWhiteSpace(task.Summary) &&
                task.Summary.Length <= 1200 && task.DeadlineEvidence?.Length <= 1000 &&
                task.EstimatedMinutes is >= 1 and <= 43200 && task.Requirements is { Length: > 0 } && task.Deliverables is { Length: > 0 } &&
                task.AcceptanceCriteria is { Length: > 0 } && ValidItems(task.Requirements, 20, 500) && ValidItems(task.Deliverables, 15, 500) &&
                ValidItems(task.AcceptanceCriteria, 15, 500) && ValidItems(task.Notes, 15, 500) && ValidItems(task.SourceReferences, 10, 300),
                "TEAM_BRIEF_INCOMPLETE", "Có task chưa đủ nội dung, đầu ra hoặc tiêu chí hoàn thành. Hãy yêu cầu NORA bổ sung.", 503);
            if (task.Deadline is null || task.Deadline <= Now || task.Deadline > Now.AddDays(365) || string.IsNullOrWhiteSpace(task.DeadlineEvidence) ||
                document?.PdfData == null && !source.Contains(task.DeadlineEvidence, StringComparison.OrdinalIgnoreCase))
                return new(Guid.Empty, result.Summary, $"Hãy làm rõ deadline cho “{task.Name}” trong tài liệu hoặc ô mô tả. NORA cần hạn chót có căn cứ trước khi tạo task.",
                    result.SplitReason, members.Length, document?.Name, [], result.Warnings);
            var description = BriefDescription(task, document?.Name);
            Require(description.Length <= 4000, "TEAM_BRIEF_TOO_DETAILED", "Yêu cầu một gói việc quá dài để lưu đầy đủ. Hãy tách tài liệu theo hạng mục và phân tích lại.");
            tasks.Add(new(Guid.NewGuid(), task.Name.Trim(), description, task.EstimatedMinutes, task.Deadline.Value,
                2, true, null, 0, "Open", Guid.NewGuid(), false));
        }
        var row = new PrivatePlanningProposal { UserId = uid, Kind = "TEAM_BRIEF", ExpiresAt = Now.AddMinutes(30),
            PayloadJson = JsonSerializer.Serialize(new TeamBriefPayload(teamId, members, tasks.ToArray())) };
        db.PrivatePlanningProposals.Add(row); await db.SaveChangesAsync(ct);
        return new(row.Id, result.Summary, "", result.SplitReason, members.Length, document?.Name, tasks.ToArray(),
            result.Warnings.Append("Thời lượng là ước lượng để trưởng nhóm xác nhận. Task chỉ được giao sau khi xác nhận danh sách; thành viên tự nhận để NORA xếp lịch cá nhân.").ToArray());
    }

    private static string BriefDescription(TeamBriefTask task, string? fileName)
    {
        var text = new StringBuilder().AppendLine("TÓM TẮT").AppendLine(task.Summary.Trim());
        void Section(string title, string[] items)
        {
            if (items.Length == 0) return;
            text.AppendLine().AppendLine(title);
            foreach (var item in items) text.Append("• ").AppendLine(item.Trim());
        }
        Section("YÊU CẦU CẦN THỰC HIỆN", task.Requirements);
        Section("ĐẦU RA CẦN BÀN GIAO", task.Deliverables);
        Section("TIÊU CHÍ HOÀN THÀNH", task.AcceptanceCriteria);
        Section("GHI CHÚ & PHỐI HỢP", task.Notes);
        Section("THAM CHIẾU TÀI LIỆU", task.SourceReferences);
        text.AppendLine().Append("CĂN CỨ DEADLINE: ").AppendLine(task.DeadlineEvidence);
        if (fileName != null) text.Append("TÀI LIỆU NGUỒN: ").AppendLine(fileName);
        return text.ToString().Trim();
    }

    public Task<TeamBriefConfirmView> ConfirmTeamBriefAsync(Guid uid, Guid teamId, Guid proposalId, CancellationToken ct) => Mutate(uid, async () =>
    {
        await TeamMember(uid, teamId, ct, true);
        var row = await Find<PrivatePlanningProposal>(uid, proposalId, ct);
        var payload = JsonSerializer.Deserialize<TeamBriefPayload>(row.PayloadJson);
        Require(row.Kind == "TEAM_BRIEF" && payload != null && payload.TeamId == teamId, "NOT_FOUND", "Không tìm thấy bản chia việc.", 404);
        if (row.Applied) return new TeamBriefConfirmView(payload.Tasks.Select(x => x.Id).ToArray());
        Require(row.ExpiresAt > Now && payload.Tasks.All(x => x.Deadline > Now), "PROPOSAL_EXPIRED", "Bản chia việc đã hết hạn. Hãy phân tích lại.", 409);
        Require(payload.Members.SequenceEqual(await BriefMembers(teamId, ct)), "TEAM_MEMBERS_CHANGED", "Thành viên nhóm đã thay đổi. Hãy chia việc lại theo số thành viên mới.", 409);
        Require(await db.TeamTasks.CountAsync(x => x.TeamId == teamId && x.Status != "Completed", ct) + payload.Tasks.Length <= 500,
            "TEAM_TASK_LIMIT", "Nhóm đang có quá nhiều task. Hãy hoàn thành hoặc xử lý công việc cũ trước.");
        foreach (var task in payload.Tasks)
        {
            db.TeamTasks.Add(new() { Id = task.Id, TeamId = teamId, Name = task.Name, Description = task.Description,
                EstimatedMinutes = task.EstimatedMinutes, Deadline = task.Deadline, Priority = task.Priority, Splittable = task.Splittable });
            TeamLog(uid, teamId, task.Id, "TASK_CREATED");
        }
        row.Applied = true;
        (await TeamMember(uid, teamId, ct, true)).MutationVersion = Guid.NewGuid();
        return new TeamBriefConfirmView(payload.Tasks.Select(x => x.Id).ToArray());
    }, ct, false, System.Data.IsolationLevel.Serializable);
}
