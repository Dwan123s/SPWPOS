using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/calendar/move"),Tags("Calendar")]
public sealed class CalendarMoveController(IWorkspaceService service):WorkspaceController
{
    [HttpPost] public async Task<IActionResult> Move(CalendarMoveInput input,CancellationToken ct)=>Data(await service.MoveCalendarItemAsync(UserId,input,ct));
}
