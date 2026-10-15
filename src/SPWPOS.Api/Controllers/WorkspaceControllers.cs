using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Authorize, ApiController]
public abstract class WorkspaceController : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    protected IActionResult Data<T>(T data) => Ok(ApiResponse<T>.Ok(data));
    protected IActionResult Done() => Data(new MessageResponse("Đã cập nhật thành công."));
}
[Route("api/workspace"), Tags("Workspace")]
public sealed class DashboardController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data(await service.GetAsync(UserId, ct));
    [HttpPut("profile")] public async Task<IActionResult> Profile(ProfileInput input, CancellationToken ct) { await service.UpdateProfileAsync(UserId, input, ct); return Done(); }
}
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
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, TaskInput input, CancellationToken ct) { await service.SaveTaskAsync(UserId, id, input, ct); return Done(); }
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
[Route("api/categories"), Tags("Tasks")]
public sealed class CategoriesController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data((await service.GetAsync(UserId, ct)).Categories);
    [HttpPost] public async Task<IActionResult> Create(CategoryInput input, CancellationToken ct) => Data(new { id = await service.SaveCategoryAsync(UserId, null, input, ct) });
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CategoryInput input, CancellationToken ct) { await service.SaveCategoryAsync(UserId, id, input, ct); return Done(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteCategoryAsync(UserId, id, ct); return Done(); }
}
[Route("api/time"), Tags("Time & Calendar")]
public sealed class TimeController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("calendar")] public async Task<IActionResult> Calendar(CancellationToken ct) { var state = await service.GetAsync(UserId, ct); return Data(new { state.Events, state.Sessions }); }
    [HttpPut("availability")] public async Task<IActionResult> Weekly(WeeklyInput input, CancellationToken ct) { await service.SetAvailabilityAsync(UserId, input, ct); return Done(); }
    [HttpPost("exceptions")] public async Task<IActionResult> AddException(ExceptionInput input, CancellationToken ct) => Data(new { id = await service.SaveExceptionAsync(UserId, null, input, ct) });
    [HttpPut("exceptions/{id:guid}")] public async Task<IActionResult> UpdateException(Guid id, ExceptionInput input, CancellationToken ct) { await service.SaveExceptionAsync(UserId, id, input, ct); return Done(); }
    [HttpDelete("exceptions/{id:guid}")] public async Task<IActionResult> DeleteException(Guid id, CancellationToken ct) { await service.DeleteExceptionAsync(UserId, id, ct); return Done(); }
    [HttpPost("events")] public async Task<IActionResult> AddEvent(EventInput input, CancellationToken ct) => Data(new { id = await service.SaveEventAsync(UserId, null, input, ct) });
    [HttpPut("events/{id:guid}")] public async Task<IActionResult> UpdateEvent(Guid id, EventInput input, CancellationToken ct) { await service.SaveEventAsync(UserId, id, input, ct); return Done(); }
    [HttpDelete("events/{id:guid}")] public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken ct) { await service.DeleteEventAsync(UserId, id, ct); return Done(); }
    [HttpPut("preferences")] public async Task<IActionResult> Preferences(PreferenceInput input, CancellationToken ct) { await service.SetPreferenceAsync(UserId, input, ct); return Done(); }
}
[Route("api/plans"), Tags("Planning")]
public sealed class PlansController(IWorkspaceService service) : WorkspaceController
{
    [HttpPost] public async Task<IActionResult> Generate(PlanInput input, CancellationToken ct) => Data(await service.GeneratePlanAsync(UserId, input, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Review(Guid id, CancellationToken ct) => Data(await service.GetPlanAsync(UserId, id, ct));
    [HttpGet("{id:guid}/items")] public async Task<IActionResult> Decisions(Guid id, CancellationToken ct) => Data(await service.GetPlanItemsAsync(UserId, id, ct));
    [HttpPost("{id:guid}/discard")] public async Task<IActionResult> Discard(Guid id, CancellationToken ct) { await service.DiscardPlanAsync(UserId, id, ct); return Done(); }
    [HttpPost("{id:guid}/apply")] public async Task<IActionResult> Apply(Guid id, CancellationToken ct) { await service.ApplyPlanAsync(UserId, id, ct); return Done(); }
    [HttpPost("reschedule")] public async Task<IActionResult> Reschedule(PlanInput input, CancellationToken ct) => Data(await service.GeneratePlanAsync(UserId, input with { IsReschedule = true }, ct));
    [HttpPost("conflicts")] public async Task<IActionResult> Conflicts(PlanInput input, CancellationToken ct) => Data(await service.DetectConflictsAsync(UserId, input, ct));
    [HttpPost("scenario")] public async Task<IActionResult> Scenario(ScenarioInput input, CancellationToken ct) => Data(await service.ScenarioAsync(UserId, input, ct));
}
[Route("api/sessions"), Tags("Execution")]
public sealed class SessionsController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("{id:guid}/execution")] public async Task<IActionResult> History(Guid id, CancellationToken ct) => Data(await service.GetExecutionAsync(UserId, id, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Move(Guid id, SessionEdit input, CancellationToken ct) { await service.EditSessionAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("{id:guid}/lock")] public async Task<IActionResult> Lock(Guid id, CancellationToken ct) { await service.LockSessionAsync(UserId, id, true, ct); return Done(); }
    [HttpPost("{id:guid}/unlock")] public async Task<IActionResult> Unlock(Guid id, CancellationToken ct) { await service.LockSessionAsync(UserId, id, false, ct); return Done(); }
    [HttpPost("{id:guid}/split")] public async Task<IActionResult> Split(Guid id, SplitInput input, CancellationToken ct) { await service.SplitSessionAsync(UserId, id, input, ct); return Done(); }
    [HttpPut("{id:guid}/actual-duration")] public async Task<IActionResult> Duration(Guid id, DurationInput input, CancellationToken ct) { await service.RecordDurationAsync(UserId, id, input, ct); return Done(); }
    [HttpPost("{id:guid}/{sessionAction:regex(^(start|pause|resume|complete|skip)$)}")] public async Task<IActionResult> Execute(Guid id, [FromRoute(Name = "sessionAction")] string action, CancellationToken ct) { await service.ExecuteSessionAsync(UserId, id, action, ct); return Done(); }
}
[Route("api/intelligence"), Tags("Insights")]
public sealed class InsightsController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("history")] public async Task<IActionResult> History([FromQuery] AnalyticsInput input, CancellationToken ct) => Data(await service.GetAnalyticsAsync(UserId, input, ct));
    [HttpGet("tasks")] public async Task<IActionResult> Tasks(CancellationToken ct, int page = 1, int pageSize = 20, string? search = null) => Data(await service.GetTaskPerformanceAsync(UserId, page, pageSize, search, ct));
    [HttpPost] public async Task<IActionResult> Analyze(PlanInput input, CancellationToken ct) => Data(await service.AnalyzeAsync(UserId, input, ct));
}
[Route("api/notifications"), Tags("Notifications")]
public sealed class NotificationsController(IWorkspaceService service) : WorkspaceController
{
    [HttpPut("settings")] public async Task<IActionResult> Settings(NotificationSettingInput input, CancellationToken ct) { await service.SetNotificationSettingsAsync(UserId, input, ct); return Done(); }
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data((await service.GetAsync(UserId, ct)).Notifications);
    [HttpPost("{id:guid}/read")] public async Task<IActionResult> Read(Guid id, CancellationToken ct) { await service.MarkNotificationAsync(UserId, id, ct); return Done(); }
}
[Authorize(Roles = "Admin"), Route("api/admin"), Tags("Administration")]
public sealed class AdministrationController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet("users")] public async Task<IActionResult> Users(CancellationToken ct, string? search = null) => Data(await service.SearchUsersAsync(search, ct));
    [HttpPost("users/{id:guid}/lock")] public async Task<IActionResult> Lock(Guid id, CancellationToken ct, [FromBody] AccountStatusInput? input = null) { await service.SetUserActiveAsync(UserId, id, false, ct, input?.Reason); return Done(); }
    [HttpPost("users/{id:guid}/unlock")] public async Task<IActionResult> Unlock(Guid id, CancellationToken ct, [FromBody] AccountStatusInput? input = null) { await service.SetUserActiveAsync(UserId, id, true, ct, input?.Reason); return Done(); }
    [HttpGet("users/{id:guid}/status-logs")] public async Task<IActionResult> AccountLogs(Guid id, CancellationToken ct) => Data(await service.GetAccountStatusLogsAsync(id, ct));
    [HttpGet("jobs")] public async Task<IActionResult> Jobs(CancellationToken ct) => Data(await service.GetJobRunsAsync(ct));
    [HttpGet("status")] public async Task<IActionResult> Status(CancellationToken ct) => Data(await service.SystemStatusAsync(ct));
}
