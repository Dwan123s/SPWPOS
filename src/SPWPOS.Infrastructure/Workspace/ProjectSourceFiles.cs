using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static ProjectSourceDocument ReadProjectSource(Guid projectId, TeamBriefFile file)
    {
        var document = TeamBriefReader.Read(file);
        var name = new string(document.Name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        Require(name.Length > 0, "VALIDATION_ERROR", "Tên tài liệu không hợp lệ.");
        var mime = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            _ => "text/plain"
        };
        return new() { ProjectId = projectId, Name = name, MimeType = mime, Content = Convert.FromBase64String(file.Data) };
    }

    private async Task<Guid> SourceProjectId(Guid uid, Guid taskId, CancellationToken ct)
    {
        var tasks = Mine<WorkTask>(uid).Where(task => !task.IsDeleted &&
            !db.TeamTaskAssignments.Any(assignment => assignment.Active && assignment.UserId == uid && assignment.WorkTaskId == task.Id));
        var projectId = await (from task in tasks
            join project in tasks.Where(task => task.ParentId == null) on task.ParentId ?? task.Id equals project.Id
            where task.Id == taskId
            select (Guid?)project.Id).SingleOrDefaultAsync(ct);
        Require(projectId.HasValue, "NOT_FOUND", "Không tìm thấy dự án của công việc này.", 404);
        return projectId.GetValueOrDefault();
    }

    public Task<string> AttachProjectSourceAsync(Guid uid, Guid taskId, TeamBriefFile input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var projectId = await SourceProjectId(uid, taskId, ct);
        Require(await Mine<WorkTask>(uid).AnyAsync(task => !task.IsDeleted && task.ParentId == projectId, ct),
            "NOT_FOUND", "Không tìm thấy dự án của công việc này.", 404);
        var document = ReadProjectSource(projectId, input);
        var previous = await db.ProjectSourceDocuments.AsNoTracking().SingleOrDefaultAsync(file => file.ProjectId == projectId, ct);
        if (previous is not null)
        {
            Require(previous.Name == document.Name && previous.Content.SequenceEqual(document.Content),
                "PROJECT_SOURCE_EXISTS", "Dự án đã có tài liệu nguồn. Hãy tải lại để xem.", 409);
            return previous.Name;
        }
        db.ProjectSourceDocuments.Add(document);
        return document.Name;
    }, ct, false);

    public async Task<ProjectSourceDownload> DownloadProjectSourceAsync(Guid uid, Guid taskId, CancellationToken ct)
    {
        Require(await db.Users.AnyAsync(user => user.Id == uid && user.IsActive && user.EmailConfirmed, ct),
            "FORBIDDEN", "Tài khoản không khả dụng.", 403);
        var projectId = await SourceProjectId(uid, taskId, ct);
        var file = await db.ProjectSourceDocuments.AsNoTracking().Where(item => item.ProjectId == projectId)
            .Select(item => new ProjectSourceDownload(item.Name, item.MimeType, item.Content)).SingleOrDefaultAsync(ct);
        Require(file is not null, "NOT_FOUND", "Dự án chưa có file gốc để tải xuống.", 404);
        return file!;
    }
}
