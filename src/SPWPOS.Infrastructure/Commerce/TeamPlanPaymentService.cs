using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;

namespace SPWPOS.Infrastructure.Commerce;

public sealed class TeamPlanPaymentService(ApplicationDbContext db, IPayOsGateway gateway,
    IOptions<PayOsOptions> options, TimeProvider clock)
{
    private static AuthException Error(string code, string message, int status = 409) => new(code, message, status);
    private DateTimeOffset Now => clock.GetUtcNow();
    private async Task AuthorizeAsync(Guid uid, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct))
            throw Error("FORBIDDEN", "Tài khoản không khả dụng.", 403);
    }
    private async Task AuthorizeAsync(Guid uid, Guid teamId, CancellationToken ct)
    {
        if (!await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.UserId == uid && x.Active && x.Role == "Leader", ct) ||
            !await db.Users.AnyAsync(x => x.Id == uid && x.IsActive && x.EmailConfirmed, ct))
            throw Error("FORBIDDEN", "Chỉ trưởng nhóm mới có thể quản lý gói của nhóm.", 403);
    }

    private TeamPlanOrderView View(TeamPlanPurchase order) => new(order.PlanCode,
        order.PaidAt != null ? "PAID" : order.ClosedStatus ?? (order.ExpiresAt <= Now ? "EXPIRED" : "PENDING"), order.CheckoutUrl);

    public async Task<TeamPlanBillingView> GetAsync(Guid uid, Guid teamId, CancellationToken ct)
    {
        await AuthorizeAsync(uid, teamId, ct);
        return await GetAsync(uid, ct);
    }

    public async Task<TeamPlanBillingView> GetAsync(Guid uid, CancellationToken ct)
    {
        await AuthorizeAsync(uid, ct);
        var pending = await db.TeamPlanPurchases.AsNoTracking().Where(x => x.UserId == uid && x.PaidAt == null && x.ClosedStatus == null)
            .OrderBy(x => x.CreatedAt).ToArrayAsync(ct);
        foreach (var order in pending) await ReconcileAsync(order, ct);
        var latest = await db.TeamPlanPurchases.AsNoTracking().Where(x => x.UserId == uid)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        var plan = await TeamPlans.ForAccountAsync(db, uid, Now, ct);
        return new(options.Value.Ready, TeamPlans.Catalog, latest is null ? null : View(latest), plan,
            await TeamPlans.ValidUntilAsync(db, uid, plan.Code, ct));
    }

    public async Task<TeamPlanOrderView> CreateAsync(Guid uid, Guid teamId, TeamPlanCheckoutInput input, CancellationToken ct)
    {
        await AuthorizeAsync(uid, teamId, ct);
        return await CreateAsync(uid, input, ct);
    }

    public async Task<TeamPlanOrderView> CreateAsync(Guid uid, TeamPlanCheckoutInput input, CancellationToken ct)
    {
        await AuthorizeAsync(uid, ct);
        if (!options.Value.Ready) throw Error("PAYMENTS_NOT_CONFIGURED", "Thanh toán payOS chưa sẵn sàng.", 503);
        var plan = TeamPlans.Catalog.SingleOrDefault(x => x.Code == input.Code && x.AmountVnd > 0)
            ?? throw Error("INVALID_PLAN", "Chọn gói Premium hợp lệ.", 400);
        await GetAsync(uid, ct);
        TeamPlanPurchase order;
        await using (var tx = await AccountTransaction.BeginAsync(db, "premium:" + uid, ct))
        {
            await AuthorizeAsync(uid, ct);
            var current = await TeamPlans.ForAccountAsync(db, uid, Now, ct);
            if (current.AmountVnd > plan.AmountVnd) throw Error("PLAN_ALREADY_OWNED", "Tài khoản đang có gói cao hơn. Bạn có thể gia hạn gói hiện tại.");
            var existing = await db.TeamPlanPurchases.Where(x => x.UserId == uid && x.PaidAt == null &&
                x.ClosedStatus == null && x.ExpiresAt > Now).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            if (existing != null && existing.PlanCode != plan.Code)
                throw Error("PAYMENT_PENDING", "Tài khoản đang có đơn thanh toán khác. Hãy hoàn tất hoặc chờ đơn hết hạn trước khi đổi gói.");
            order = existing ?? new TeamPlanPurchase
            {
                UserId = uid, PlanCode = plan.Code, AmountVnd = plan.AmountVnd,
                CreatedAt = Now, ExpiresAt = Now.AddMinutes(15),
                OrderCode = (BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8)) & 0x000FFFFFFFFFFFFF) + 1
            };
            if (existing is null) db.TeamPlanPurchases.Add(order);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        if (order.CheckoutUrl != null) return View(order);
        var link = await gateway.FindAsync(order.OrderCode, ct) ?? await gateway.CreateAsync(order.OrderCode, order.AmountVnd,
            order.ExpiresAt, ct, new(plan.MemberLimit is int limit ? $"Premium {limit} thanh vien" : "Premium khong gioi han"));
        if (link.OrderCode != order.OrderCode || link.AmountVnd != order.AmountVnd || link.Currency != "VND" ||
            string.IsNullOrWhiteSpace(link.Id) || link.Id.Length > 100 ||
            !Uri.TryCreate(link.CheckoutUrl, UriKind.Absolute, out var url) || url.Scheme != "https" ||
            url.Host != "pay.payos.vn" || url.UserInfo.Length > 0 || !url.IsDefaultPort || url.AbsoluteUri.Length > 512)
            throw Error("PAYMENT_LINK_INVALID", "Liên kết thanh toán không khớp đơn hàng.", 502);
        await using (var tx = await AccountTransaction.BeginAsync(db, "premium:" + uid, ct))
        {
            await db.Entry(order).ReloadAsync(ct);
            order.PaymentLinkId = link.Id;
            order.CheckoutUrl = url.AbsoluteUri;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return View(order);
    }

    public async Task HandleWebhookAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (!options.Value.Ready) throw Error("PAYMENTS_NOT_CONFIGURED", "Thanh toán chưa sẵn sàng.", 503);
        var receipt = PayOsPaymentSignatures.VerifyWebhook(body, options.Value.ChecksumKey)
            ?? throw Error("PAYMENT_SIGNATURE_INVALID", "Chữ ký payOS không hợp lệ.", 400);
        if (!receipt.Succeeded) return;
        var order = await db.TeamPlanPurchases.AsNoTracking().SingleOrDefaultAsync(x => x.OrderCode == receipt.OrderCode, ct);
        if (order is null) return;
        if (receipt.AmountVnd != order.AmountVnd || receipt.Currency != "VND" ||
            order.PaymentLinkId != null && receipt.PaymentLinkId != order.PaymentLinkId)
            throw Error("PAYMENT_MISMATCH", "Giao dịch không khớp đơn nâng cấp nhóm.");
        await ReconcileAsync(order, ct);
        if (!await db.TeamPlanPurchases.AnyAsync(x => x.Id == order.Id && x.PaidAt != null, ct))
            throw Error("PAYMENT_PENDING", "Đang chờ payOS xác nhận thanh toán.");
    }

    private async Task ReconcileAsync(TeamPlanPurchase found, CancellationToken ct)
    {
        if (!options.Value.Ready || found.PaidAt != null) return;
        var payment = await gateway.GetPaymentAsync(found.OrderCode, ct);
        if (payment is null) return;
        var plan = TeamPlans.Catalog.SingleOrDefault(x => x.Code == found.PlanCode && x.AmountVnd == found.AmountVnd && x.AmountVnd > 0);
        if (plan is null || payment.OrderCode != found.OrderCode || found.PaymentLinkId != null && payment.Id != found.PaymentLinkId || payment.AmountVnd != found.AmountVnd)
            throw Error("PAYMENT_MISMATCH", "Trạng thái thanh toán không khớp đơn nâng cấp nhóm.");
        if (payment.Status is not ("PAID" or "CANCELLED" or "EXPIRED")) return;
        if (payment.Status == "PAID" && (payment.AmountPaid < found.AmountVnd || payment.AmountRemaining != 0 || payment.Transactions.Length == 0 ||
            payment.Transactions.Sum(x => (long)x.Amount) != payment.AmountPaid ||
            payment.Transactions.Any(x => x.Amount <= 0 || string.IsNullOrWhiteSpace(x.Reference) || x.Reference.Length > 100 || string.IsNullOrWhiteSpace(x.AccountNumber)) ||
            payment.Transactions.Select(x => x.Reference).Distinct(StringComparer.Ordinal).Count() != payment.Transactions.Length))
            throw Error("PAYMENT_MISMATCH", "Chưa xác nhận được số tiền và giao dịch thanh toán.");
        await using var tx = await AccountTransaction.BeginAsync(db, "premium:" + found.UserId, ct);
        var order = await db.TeamPlanPurchases.SingleAsync(x => x.Id == found.Id, ct);
        await db.Entry(order).ReloadAsync(ct);
        if (order.PaidAt != null) return;
        if (payment.Status == "PAID")
        {
            var references = payment.Transactions.Select(x => x.Reference).ToArray();
            if (await db.TeamPlanPurchases.AnyAsync(x => x.Id != order.Id && x.ReceiptReference != null && references.Contains(x.ReceiptReference), ct) ||
                await db.SkinPurchases.AnyAsync(x => x.ReceiptReference != null && references.Contains(x.ReceiptReference), ct))
                throw Error("PAYMENT_RECEIPT_REUSED", "Giao dịch đã được xử lý.");
            var previousExpiry = await TeamPlans.ValidUntilAsync(db, order.UserId, order.PlanCode, ct);
            var startsAt = previousExpiry > Now ? previousExpiry.Value : Now;
            order.PaidAt = Now;
            order.ValidUntil = startsAt.AddMonths(1);
            order.ReceiptReference = payment.Transactions[^1].Reference;
            order.PaymentLinkId = payment.Id;
            order.ClosedStatus = null;
        }
        else order.ClosedStatus = payment.Status;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
