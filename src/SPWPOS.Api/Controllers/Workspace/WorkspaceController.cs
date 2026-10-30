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
