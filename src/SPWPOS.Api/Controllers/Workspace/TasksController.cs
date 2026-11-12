using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/tasks"), Tags("Tasks")]
public sealed class TasksController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct, string? search = null, string? status = null, Guid? categoryId = null, int? priority = null, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var tasks = (await service.GetAsync(UserId, ct)).Tasks.Where(x => (string.IsNullOrWhiteSpace(search) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (status is null || x.Status.Equals(status, StringComparison.OrdinalIgnoreCase)) && (categoryId is null || x.CategoryId == categoryId) && (priority is null || x.Priority == priority) &&
            (from is null || x.Deadline >= from) && (to is null || x.Deadline <= to)).ToArray(); return Data(tasks);
    }
    [HttpGet("{id:guid}")] public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Data((await service.GetAsync(UserId, ct)).Tasks.SingleOrDefault(x => x.Id == id) ?? throw new AuthException("NOT_FOUND", "Không tìm thấy công việc.", 404));
    [HttpPost] public async Task<IActionResult> Create(TaskInput input, CancellationToken ct) => Data(new { id = await service.SaveTaskAsync(UserId, null, input, ct) });
    [HttpPost("preview-text")] public async Task<IActionResult> PreviewText(CaptureInput input, CancellationToken ct) =>
        Data(await service.PreviewTaskTextAsync(UserId, input, ct));
    [HttpPost("batch/preview"), RequestSizeLimit(12000000)]
    public async Task<IActionResult> PreviewBatch(TeamBriefInput input, CancellationToken ct) =>
        Data(await service.PreviewTaskBatchAsync(UserId, input, ct));
    [HttpPost("batch"), RequestSizeLimit(12000000)] public async Task<IActionResult> SaveBatch(TaskBatchInput input, CancellationToken ct) =>
        Data(await service.SaveTaskBatchAsync(UserId, input, ct));
    [HttpPut("{id:guid}/source"), RequestSizeLimit(12000000)]
    public async Task<IActionResult> AttachSource(Guid id, TeamBriefFile input, CancellationToken ct) =>
        Data(new { name = await service.AttachProjectSourceAsync(UserId, id, input, ct) });
    [HttpGet("{id:guid}/source"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DownloadSource(Guid id, CancellationToken ct)
    {
        var file = await service.DownloadProjectSourceAsync(UserId, id, ct);
        return File(file.Content, file.MimeType, file.Name);
    }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, TaskInput input, CancellationToken ct) { await service.SaveTaskAsync(UserId, id, input, ct); return Done(); }
    [HttpPut("{id:guid}/checklist")] public async Task<IActionResult> Checklist(Guid id, MatrixChecklistInput input, CancellationToken ct) =>
        Data(await service.UpdateTaskChecklistAsync(UserId, id, input, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteTaskAsync(UserId, id, ct); return Done(); }
    [HttpPatch("{id:guid}/status")] public async Task<IActionResult> Status(Guid id, StatusInput input, CancellationToken ct) { await service.SetStatusAsync(UserId, id, input, ct); return Done(); }
    [HttpPatch("{id:guid}/progress")] public async Task<IActionResult> Progress(Guid id, ProgressInput input, CancellationToken ct)
    {
        var task = (await service.GetAsync(UserId, ct)).Tasks.SingleOrDefault(x => x.Id == id) ?? throw new AuthException("NOT_FOUND", "Không tìm thấy công việc.", 404);
        await service.SetStatusAsync(UserId, id, new(input.Progress == 100 ? "Completed" : task.Status == "Completed" ? "InProgress" : task.Status, input.Progress), ct); return Done();
    }
    [HttpPost("{id:guid}/dependencies/{predecessor:guid}")] public async Task<IActionResult> AddDependency(Guid id, Guid predecessor, CancellationToken ct) { await service.SetDependencyAsync(UserId, id, predecessor, false, ct); return Done(); }
    [HttpDelete("{id:guid}/dependencies/{predecessor:guid}")] public async Task<IActionResult> RemoveDependency(Guid id, Guid predecessor, CancellationToken ct) { await service.SetDependencyAsync(UserId, id, predecessor, true, ct); return Done(); }
    [HttpPost("capture")] public async Task<IActionResult> Capture(CaptureInput input, CancellationToken ct) => Data(await service.CaptureAsync(UserId, input, ct));
    [HttpPost("{id:guid}/breakdown")] public async Task<IActionResult> Breakdown(Guid id, CancellationToken ct, bool useAi = false) => Data(await service.BreakdownAsync(UserId, id, useAi, ct));
}
