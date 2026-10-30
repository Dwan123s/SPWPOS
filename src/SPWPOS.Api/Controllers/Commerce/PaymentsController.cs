using Microsoft.AspNetCore.Mvc;
using SPWPOS.Application.Authentication;
namespace SPWPOS.Api.Controllers;

[Route("api/commerce/payments"), Tags("Gamification")]
public sealed class PaymentsController : WorkspaceController
{
    [HttpPost]
    public IActionResult Create() => throw new AuthException("REAL_PAYMENTS_DISABLED", "Thanh toán tiền thật đang đóng băng. Trang phục NORA được mở khóa bằng XP nội bộ.", 410);
}
