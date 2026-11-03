using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPWPOS.Infrastructure.Commerce;
namespace SPWPOS.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/commerce/payos/webhook")]
public sealed class PayOsWebhookController(SkinPaymentService payments) : ControllerBase
{
    [HttpPost, RequestSizeLimit(65536)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var body = new MemoryStream();
        var buffer = new byte[4096]; int read;
        while ((read = await Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (body.Length + read > 65536) return StatusCode(413);
            body.Write(buffer, 0, read);
        }
        await payments.HandleWebhookAsync(body.ToArray(), ct);
        return Ok(new { success = true });
    }
}
