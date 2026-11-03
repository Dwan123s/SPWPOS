using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private TeamTaskDocument SourceDocument(Guid uid, Guid teamId, TeamBriefFile file)
    {
        Require(file != null, "VALIDATION_ERROR", "Hãy chọn tài liệu cần đính kèm.");
        var parsed = TeamBriefReader.Read(file);
        var name = new string(parsed.Name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        Require(name.Length > 0, "VALIDATION_ERROR", "Tên tài liệu không hợp lệ.");
        var content = Convert.FromBase64String(file.Data);
        var mime = Path.GetExtension(name).ToLowerInvariant() switch {
            ".pdf" => "application/pdf", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".md" => "text/markdown", _ => "text/plain"
        };
        return new() { TeamId = teamId, UploadedBy = uid, Name = name, MimeType = mime, Content = content, Size = content.Length, CreatedAt = Now };
    }

    public Task AttachTeamSourceFileAsync(Guid uid, Guid taskId, TeamSourceFileInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var task = await SharedTask(uid, taskId, ct); var member = await TeamMember(uid, task.TeamId, ct, true);
        Require(input.RequestId != Guid.Empty, "VALIDATION_ERROR", "Yêu cầu gửi file không hợp lệ.");
        var document = SourceDocument(uid, task.TeamId, input.File);
        if (task.SourceDocumentId == input.RequestId)
        {
            var previous = await db.TeamTaskDocuments.AsNoTracking().SingleAsync(x => x.Id == input.RequestId, ct);
            Require(previous.Name == document.Name && previous.Content.SequenceEqual(document.Content), "REQUEST_REUSED", "Yêu cầu này đã được dùng cho tài liệu khác.", 409);
            return;
        }
        Require(task.SourceDocumentId == null, "TEAM_SOURCE_EXISTS", "Task đã có tài liệu gốc. Hãy tải lại để xem.", 409);
        Require(task.Version == input.Version, "TEAM_TASK_STALE", "Task vừa thay đổi. Hãy tải lại trước khi gửi tài liệu.", 409);
        Require(!await db.TeamTaskDocuments.AnyAsync(x => x.Id == input.RequestId, ct), "REQUEST_REUSED", "Yêu cầu gửi file đã được sử dụng.", 409);
        document.Id = input.RequestId; db.TeamTaskDocuments.Add(document);
        task.SourceDocumentId = document.Id; task.Version = Guid.NewGuid(); member.MutationVersion = Guid.NewGuid();
    }, ct, false);

    public async Task<TeamCompletionDownload> DownloadTeamSourceFileAsync(Guid uid, Guid taskId, Guid fileId, CancellationToken ct)
    {
        Require(await db.Users.AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct), "FORBIDDEN", "Tài khoản không khả dụng.", 403);
        var task = await SharedTask(uid, taskId, ct);
        Require(task.SourceDocumentId == fileId, "NOT_FOUND", "Không tìm thấy tài liệu của công việc này.", 404);
        var file = await db.TeamTaskDocuments.AsNoTracking().Where(x => x.Id == fileId && x.TeamId == task.TeamId)
            .Select(x => new { x.Name, x.MimeType }).SingleOrDefaultAsync(ct);
        Require(file != null, "NOT_FOUND", "Không tìm thấy tài liệu của công việc này.", 404);
        var content = await db.TeamTaskDocuments.Where(x => x.Id == fileId).Select(x => x.Content).SingleAsync(ct);
        return new(file.Name, file.MimeType, content);
    }
}
