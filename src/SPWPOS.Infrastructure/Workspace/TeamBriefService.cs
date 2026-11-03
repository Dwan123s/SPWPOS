using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record BriefMember(Guid UserId, Guid Version, string Role);
    private sealed record TeamBriefPayload(Guid TeamId, BriefMember[] Members, SharedTaskView[] Tasks, TeamBriefFile? SourceFile = null,
        TeamBriefPendingTask[]? PendingTasks = null, string Summary = "", string Question = "", string SplitReason = "", string[]? Warnings = null);
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
        if (result.Tasks.Length == 0)
            return new(Guid.Empty, result.Summary, string.IsNullOrWhiteSpace(result.Question) ? "Chưa đủ thông tin. Hãy bổ sung phạm vi công việc và deadline." : result.Question,
                result.SplitReason, members.Length, document?.Name, [], result.Warnings);
        Require(result.Tasks.Length == members.Length && result.Tasks.Select(x => x.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == result.Tasks.Length,
            "TEAM_BRIEF_SPLIT_INVALID", "NORA chưa chia đủ các gói việc khác nhau theo số thành viên hiện tại. Hãy yêu cầu phân tích lại.", 503);
        var source = input.Text + "\n" + document?.Text;
        var tasks = new List<SharedTaskView>();
        var pendingTasks = new List<TeamBriefPendingTask>();
        var deadlineQuestions = new List<string>();
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
            var validDeadline = task.Deadline > Now && task.Deadline <= Now.AddDays(365) && !string.IsNullOrWhiteSpace(task.DeadlineEvidence) &&
                (document?.PdfData != null || BriefSourceQuote(task.DeadlineEvidence, source));
            if (!validDeadline) deadlineQuestions.Add(task.Deadline <= Now
                ? $"Hạn của “{task.Name}” đã qua. Bạn cho mình hạn chót mới, gồm ngày và giờ nhé?"
                : $"Bạn xác nhận hạn chót, gồm ngày và giờ, cho “{task.Name}” nhé? Nếu cả nhóm có chung hạn nộp, chỉ cần trả lời một lần trong ô mô tả ở trên.");
            var description = BriefDescription(task, document?.Name);
            Require(description.Length <= 4000, "TEAM_BRIEF_TOO_DETAILED", "Yêu cầu một gói việc quá dài để lưu đầy đủ. Hãy tách tài liệu theo hạng mục và phân tích lại.");
            var id = Guid.NewGuid(); var importance = SuggestedImportance(task.Importance, document?.PdfData != null ? null : source);
            pendingTasks.Add(new(id, task.Name.Trim(), description, task.EstimatedMinutes, validDeadline ? task.Deadline : null,
                !validDeadline || !string.IsNullOrWhiteSpace(result.Question), importance));
            if (validDeadline) tasks.Add(new(id, task.Name.Trim(), description, task.EstimatedMinutes, task.Deadline!.Value,
                2, true, null, 0, "Open", Guid.NewGuid(), false,
                Importance: importance));
        }
        if (!string.IsNullOrWhiteSpace(result.Question) || deadlineQuestions.Count > 0)
        {
            var question = !string.IsNullOrWhiteSpace(result.Question) ? result.Question : deadlineQuestions[0];
            var waiting = new PrivatePlanningProposal { UserId = uid, Kind = "TEAM_BRIEF", ExpiresAt = Now.AddMinutes(30),
                PayloadJson = JsonSerializer.Serialize(new TeamBriefPayload(teamId, members, [], input.File, pendingTasks.ToArray(), result.Summary, question, result.SplitReason, result.Warnings)) };
            db.PrivatePlanningProposals.Add(waiting); await db.SaveChangesAsync(ct);
            return new(waiting.Id, result.Summary, question, result.SplitReason, members.Length, document?.Name, [], result.Warnings, pendingTasks.ToArray(), waiting.Revision);
        }
        var row = new PrivatePlanningProposal { UserId = uid, Kind = "TEAM_BRIEF", ExpiresAt = Now.AddMinutes(30),
            PayloadJson = JsonSerializer.Serialize(new TeamBriefPayload(teamId, members, tasks.ToArray(), input.File,
                Summary: result.Summary, SplitReason: result.SplitReason, Warnings: result.Warnings)) };
        db.PrivatePlanningProposals.Add(row); await db.SaveChangesAsync(ct);
        return new(row.Id, result.Summary, "", result.SplitReason, members.Length, document?.Name, tasks.ToArray(),
            result.Warnings.Append("Thời lượng là ước lượng để trưởng nhóm xác nhận. Task chỉ được giao sau khi xác nhận danh sách; thành viên tự nhận để NORA xếp lịch cá nhân.").ToArray(), Revision: row.Revision);
    }

    private static bool BriefSourceQuote(string quote, string source)
    {
        static string Normalize(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormC), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Trim();
        return Normalize(source).Contains(Normalize(quote), StringComparison.OrdinalIgnoreCase);
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

    public Task<TeamBriefConfirmView> ConfirmTeamBriefAsync(Guid uid, Guid teamId, Guid proposalId, CancellationToken ct, TeamBriefConfirmInput? input = null) => Mutate(uid, async () =>
    {
        await TeamMember(uid, teamId, ct, true);
        var row = await Find<PrivatePlanningProposal>(uid, proposalId, ct);
        var payload = JsonSerializer.Deserialize<TeamBriefPayload>(row.PayloadJson);
        Require(row.Kind == "TEAM_BRIEF" && payload != null && payload.TeamId == teamId, "NOT_FOUND", "Không tìm thấy bản chia việc.", 404);
        Require(row.Revision == Guid.Empty || input?.Revision == row.Revision, "DRAFT_STALE", "Bản nháp đã được chỉnh sửa. Hãy xem và xác nhận bản mới nhất.", 409);
        Require(payload.Tasks.Length > 0 && payload.PendingTasks is not { Length: > 0 }, "DRAFT_INCOMPLETE", "Hãy chốt hạn chót và lưu bản nháp trước khi giao việc.", 409);
        var selections = input?.Recipients ?? [];
        Require(selections.Length <= 20 && selections.All(x => x != null && payload.Tasks.Any(t => t.Id == x.TaskId)) &&
            selections.Select(x => x.TaskId).Distinct().Count() == selections.Length, "INVALID_RECIPIENTS", "Danh sách người nhận không hợp lệ.");
        var recipients = selections.ToDictionary(x => x.TaskId, x => x.UserId);
        if (row.Applied) {
            Require(payload.Tasks.All(t => t.DesignatedUserId == recipients.GetValueOrDefault(t.Id)), "REQUEST_REUSED", "Bản chia việc đã được giao với lựa chọn người nhận khác. Hãy tải lại nhóm.", 409);
            return new TeamBriefConfirmView(payload.Tasks.Select(x => x.Id).ToArray());
        }
        Require(row.ExpiresAt > Now && payload.Tasks.All(x => x.Deadline > Now), "PROPOSAL_EXPIRED", "Bản chia việc đã hết hạn. Hãy phân tích lại.", 409);
        Require(payload.Members.SequenceEqual(await BriefMembers(teamId, ct)), "TEAM_MEMBERS_CHANGED", "Thành viên nhóm đã thay đổi. Hãy chia việc lại theo số thành viên mới.", 409);
        Require(await db.TeamTasks.CountAsync(x => x.TeamId == teamId && x.Status != "Completed", ct) + payload.Tasks.Length <= 500,
            "TEAM_TASK_LIMIT", "Nhóm đang có quá nhiều task. Hãy hoàn thành hoặc xử lý công việc cũ trước.");
        foreach (var recipient in recipients.Values.Distinct()) await ValidateDesignatedMember(teamId, recipient, ct);
        Guid? sourceId = null;
        if (payload.SourceFile != null)
        {
            var document = SourceDocument(uid, teamId, payload.SourceFile);
            sourceId = row.Id;
            document.Id = row.Id; db.TeamTaskDocuments.Add(document);
        }
        foreach (var task in payload.Tasks)
        {
            var created = new TeamTask { Id = task.Id, TeamId = teamId, Name = task.Name, Description = task.Description,
                EstimatedMinutes = task.EstimatedMinutes, Deadline = task.Deadline, Priority = task.Priority, Splittable = task.Splittable,
                SourceDocumentId = sourceId, DesignatedUserId = recipients.GetValueOrDefault(task.Id) };
            WriteImportance(created, task.Importance, Now, "Leader"); db.TeamTasks.Add(created); await NotifyDesignatedMember(created, ct);
            TeamLog(uid, teamId, task.Id, "TASK_CREATED");
        }
        row.Applied = true;
        // The binary is stored once for the whole batch, not copied into every task or retained in applied draft JSON.
        row.PayloadJson = JsonSerializer.Serialize(payload with { SourceFile = null,
            Tasks = payload.Tasks.Select(t => t with { DesignatedUserId = recipients.GetValueOrDefault(t.Id) }).ToArray() });
        (await TeamMember(uid, teamId, ct, true)).MutationVersion = Guid.NewGuid();
        return new TeamBriefConfirmView(payload.Tasks.Select(x => x.Id).ToArray());
    }, ct, false, System.Data.IsolationLevel.Serializable);
}
