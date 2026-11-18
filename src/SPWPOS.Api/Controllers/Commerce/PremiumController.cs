using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Commerce;

namespace SPWPOS.Api.Controllers;

[Route("api/commerce/premium")]
public sealed class PremiumController(TeamPlanPaymentService payments) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Data(await payments.GetAsync(UserId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(TeamPlanCheckoutInput input, CancellationToken ct) => Data(await payments.CreateAsync(UserId, input, ct));
}
