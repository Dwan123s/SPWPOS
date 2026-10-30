using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Api.Controllers;

[Route("api/categories"), Tags("Tasks")]
public sealed class CategoriesController(IWorkspaceService service) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Data((await service.GetAsync(UserId, ct)).Categories);
    [HttpPost] public async Task<IActionResult> Create(CategoryInput input, CancellationToken ct) => Data(new { id = await service.SaveCategoryAsync(UserId, null, input, ct) });
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CategoryInput input, CancellationToken ct) { await service.SaveCategoryAsync(UserId, id, input, ct); return Done(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteCategoryAsync(UserId, id, ct); return Done(); }
}
