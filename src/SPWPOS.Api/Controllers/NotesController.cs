using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/notes"), Tags("Notes")]
public sealed class NotesController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("agent")] public IActionResult Agent() => Data(service.GetNoteAgentCapabilities());
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Data(await service.GetNotesAsync(UserId, ct));
    [HttpPost] public async Task<IActionResult> Create(NoteInput input, CancellationToken ct) => Data(await service.SaveNoteAsync(UserId, null, input, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, NoteInput input, CancellationToken ct) => Data(await service.SaveNoteAsync(UserId, id, input, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, Guid version, CancellationToken ct)
    { await service.DeleteNoteAsync(UserId, id, version, ct); return Done(); }
    [HttpPost("{id:guid}/analyze")] public async Task<IActionResult> Analyze(Guid id, NoteAnalysisInput input, CancellationToken ct) => Data(await service.AnalyzeNoteAsync(UserId, id, input, ct));
    [HttpPost("{id:guid}/tasks")] public async Task<IActionResult> Import(Guid id, NoteImportInput input, CancellationToken ct) => Data(await service.ImportNoteAsync(UserId, id, input, ct));
    [HttpPost("{id:guid}/schedule")] public async Task<IActionResult> Schedule(Guid id, NoteScheduleInput input, CancellationToken ct) => Data(await service.ScheduleNoteAsync(UserId, id, input, ct));
}
