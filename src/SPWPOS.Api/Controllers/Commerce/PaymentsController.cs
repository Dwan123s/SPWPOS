using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Commerce;
namespace SPWPOS.Api.Controllers;

[Route("api/commerce/payments"), Tags("Gamification")]
public sealed class PaymentsController(SkinPaymentService payments) : WorkspaceController
{
    [HttpGet("capabilities")]
    public IActionResult Capabilities() => Data(new { available = payments.Available, provider = "payOS", currency = "VND", skinCode = "fox", amountVnd = 10000 });
    [HttpPost]
    public async Task<IActionResult> Create(SkinCheckoutInput input, CancellationToken ct) => Data(await payments.CreateAsync(UserId, input, ct));
    [HttpGet("latest")]
    public async Task<IActionResult> Latest(CancellationToken ct) => Data(await payments.LatestAsync(UserId, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Data(await payments.GetAsync(UserId, id, ct));
}
