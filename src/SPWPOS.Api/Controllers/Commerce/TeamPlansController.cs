using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Commerce;

namespace SPWPOS.Api.Controllers;

[Route("api/teams/{teamId:guid}/billing")]
public sealed class TeamPlansController(TeamPlanPaymentService payments) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid teamId, CancellationToken ct) => Data(await payments.GetAsync(UserId, teamId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid teamId, TeamPlanCheckoutInput input, CancellationToken ct) =>
        Data(await payments.CreateAsync(UserId, teamId, input, ct));
}
