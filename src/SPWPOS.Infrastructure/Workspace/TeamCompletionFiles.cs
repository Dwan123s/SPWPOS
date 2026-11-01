using System.Text;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static readonly Dictionary<string, string> CompletionMimeTypes = new(StringComparer.OrdinalIgnoreCase) {
        [".pdf"] = "application/pdf", [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain", [".md"] = "text/markdown", [".csv"] = "text/csv", [".zip"] = "application/zip",
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp"
    };
    private TeamCompletionFile[] ValidateCompletionFiles(TeamCompletionFileInput[] files)
    {
        Require(files.Length <= 3, "TEAM_FILE_LIMIT", "Mỗi lần gửi tối đa 3 file, mỗi file 8 MB và tổng cộng 20 MB.");
        var results = new List<TeamCompletionFile>(); long total = 0;
        foreach (var input in files)
        {
            Require(input != null && !string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 180 && input.Data != null && input.Data.Length <= 11184812,
                "TEAM_FILE_INVALID", "Tên file tối đa 180 ký tự và mỗi file tối đa 8 MB.");
            var name = input.Name.Replace('\\', '/').Split('/').Last();
            Require(!string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl), "TEAM_FILE_INVALID", "Tên file không hợp lệ.");
            var extension = Path.GetExtension(name);
            Require(CompletionMimeTypes.TryGetValue(extension, out var mime), "TEAM_FILE_TYPE", "Hỗ trợ PDF, DOCX, XLSX, PPTX, TXT, MD, CSV, ZIP và ảnh PNG, JPG, WebP.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(input.Data); }
            catch (FormatException) { throw new AuthException("TEAM_FILE_INVALID", "File không đọc được. Hãy chọn lại file.", 400); }
            Require(bytes.Length is > 0 and <= 8 * 1024 * 1024 && (total += bytes.Length) <= 20 * 1024 * 1024,
                "TEAM_FILE_LIMIT", "Mỗi file tối đa 8 MB và tổng các file tối đa 20 MB.");
            bool Starts(params byte[] prefix) => bytes.AsSpan().StartsWith(prefix);
            var valid = extension.ToLowerInvariant() switch {
                ".pdf" => Starts(0x25, 0x50, 0x44, 0x46, 0x2d),
                ".docx" or ".xlsx" or ".pptx" or ".zip" => Starts(0x50, 0x4b, 0x03, 0x04) || Starts(0x50, 0x4b, 0x05, 0x06),
                ".png" => Starts(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a),
                ".jpg" or ".jpeg" => Starts(0xff, 0xd8, 0xff),
                ".webp" => bytes.Length >= 12 && Starts(0x52, 0x49, 0x46, 0x46) && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
                _ => !bytes.Contains((byte)0)
            };
            Require(valid, "TEAM_FILE_INVALID", "Nội dung file không khớp định dạng đã chọn. Hãy xuất lại file và gửi lại.");
            results.Add(new() { Name = name, MimeType = mime!, Content = bytes, Size = bytes.Length, CreatedAt = Now });
        }
        return results.ToArray();
    }

    public Task SubmitTeamCompletionAsync(Guid uid, Guid taskId, TeamCompletionSubmitInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var shared = await SharedTask(uid, taskId, ct);
        Require(input.RequestId != Guid.Empty && input.Note?.Length is not > 1000, "VALIDATION_ERROR", "Ghi chú báo cáo tối đa 1.000 ký tự.");
        var previous = await db.TeamCompletionSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.RequestId, ct);
        if (previous != null)
        {
            Require(previous.UserId == uid && previous.TeamTaskId == taskId, "TASK_STALE", "Yêu cầu gửi duyệt không còn hợp lệ. Tải lại công việc.", 409);
            return;
        }
        Require(shared.AssigneeId == uid && shared.Status == "Assigned" && shared.Version == input.Version,
            "TASK_STALE", "Chỉ người đang nhận task mới được gửi báo cáo của phiên bản hiện tại. Hãy tải lại công việc.", 409);
        var files = ValidateCompletionFiles(input.Files ?? []);
        var member = await TeamMember(uid, shared.TeamId, ct);
        shared.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid(); await db.SaveChangesAsync(ct);
        var assignment = await db.TeamTaskAssignments.SingleAsync(x => x.TeamTaskId == taskId && x.UserId == uid && x.Active, ct);
        await SetTaskStatusCore(uid, assignment.WorkTaskId, new("Completed", 100), ct, input);
        foreach (var file in files) { file.SubmissionId = input.RequestId; db.TeamCompletionFiles.Add(file); }
        (await Preference(uid, ct)).Revision = Guid.NewGuid();
    }, ct, false);

    public async Task<TeamCompletionDownload> DownloadTeamCompletionFileAsync(Guid uid, Guid taskId, Guid fileId, CancellationToken ct)
    {
        Require(await db.Users.AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct), "FORBIDDEN", "Tài khoản không khả dụng.", 403);
        var task = await SharedTask(uid, taskId, ct); var member = await TeamMember(uid, task.TeamId, ct);
        // Check metadata and permissions first; do not load binary content for unauthorized callers.
        var row = await (from file in db.TeamCompletionFiles.AsNoTracking()
            join submission in db.TeamCompletionSubmissions.AsNoTracking() on file.SubmissionId equals submission.Id
            where file.Id == fileId && submission.TeamTaskId == taskId
            select new { file.Id, file.Name, file.MimeType, submission.UserId }).SingleOrDefaultAsync(ct);
        Require(row != null, "NOT_FOUND", "Không tìm thấy file báo cáo.", 404);
        Require(member.Role == "Leader" || row.UserId == uid, "TEAM_FILE_FORBIDDEN", "Chỉ người gửi báo cáo và trưởng nhóm được tải file này.", 403);
        var content = await db.TeamCompletionFiles.Where(x => x.Id == fileId).Select(x => x.Content).SingleAsync(ct);
        return new(row.Name, row.MimeType, content);
    }
}
