using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Infrastructure.Commerce;
namespace SPWPOS.Api.Controllers;

[Authorize(Roles = "Admin"), Route("api/admin/commerce"), Tags("Administration")]
public sealed class AdminCommerceController(CommerceAdminService service) : WorkspaceController
{
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue(CancellationToken ct, DateOnly? from = null, DateOnly? to = null, string groupBy = "day") =>
        Data(await service.RevenueAsync(from, to, groupBy, ct));
    [HttpGet("purchases")]
    public async Task<IActionResult> Purchases(CancellationToken ct, DateOnly? from = null, DateOnly? to = null, string? status = null,
        string? search = null, int page = 1, int pageSize = 20) => Data(await service.PurchasesAsync(from, to, status, search, page, pageSize, ct));
}
