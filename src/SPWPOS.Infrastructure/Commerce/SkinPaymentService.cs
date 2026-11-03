using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Commerce;

public sealed class SkinPaymentService(ApplicationDbContext db, IPayOsGateway gateway, IOptions<PayOsOptions> options, TimeProvider clock)
{
    public bool Available => options.Value.Ready;
    private static AuthException Error(string code, string message, int status = 409) => new(code, message, status);
    private SkinPurchaseView View(SkinPurchase order) => new(order.Id, order.SkinCode, order.AmountVnd,
        order.Status != "PAID" && order.ExpiresAt <= clock.GetUtcNow() ? "EXPIRED" : order.Status, order.CheckoutUrl, order.ExpiresAt,
        Available ? options.Value.CheckoutReturnUrl : null);
    public async Task<SkinPurchaseView?> LatestAsync(Guid uid, CancellationToken ct)
    {
        var order = await db.SkinPurchases.AsNoTracking().Where(x => x.UserId == uid).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        return order is null ? null : View(await ReconcileAsync(order, ct));
    }
    public async Task<SkinPurchaseView> GetAsync(Guid uid, Guid id, CancellationToken ct)
    {
        var order = await db.SkinPurchases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
            ?? throw Error("NOT_FOUND", "Không tìm thấy đơn hàng.", 404);
        return View(await ReconcileAsync(order, ct));
    }
    private async Task<SkinPurchase> ReconcileAsync(SkinPurchase found, CancellationToken ct)
    {
        if (!Available || found.Status == "PAID" || found.PaymentLinkId is null) return found;
        var payment = await gateway.GetPaymentAsync(found.OrderCode, ct);
        if (payment is null) return found;
        if (payment.OrderCode != found.OrderCode || payment.Id != found.PaymentLinkId || payment.AmountVnd != found.AmountVnd || found.SkinCode != "fox")
            throw Error("PAYMENT_MISMATCH", "Trạng thái từ payOS không khớp đơn hàng.");
        if (payment.Status != "PAID") return found;
        if (payment.AmountPaid < found.AmountVnd || payment.AmountRemaining != 0 || payment.Transactions.Length == 0 ||
            payment.Transactions.Sum(x => (long)x.Amount) != payment.AmountPaid ||
            payment.Transactions.Any(x => x.Amount <= 0 || string.IsNullOrWhiteSpace(x.Reference) || x.Reference.Length > 100 ||
                string.IsNullOrWhiteSpace(x.AccountNumber)) ||
            payment.Transactions.Select(x => x.Reference).Distinct(StringComparer.Ordinal).Count() != payment.Transactions.Length)
            throw Error("PAYMENT_MISMATCH", "Chưa đối soát được số tiền và giao dịch của đơn hàng.");
        // Checkout accountNumber can be a VietQR Pro virtual alias. A signed
        // merchant lookup binds the real transactions to this exact channel,
        // orderCode and paymentLinkId; its bank number need not equal that alias.
        var account = await db.Users.AsNoTracking().SingleAsync(x => x.Id == found.UserId, ct);
        await using var tx = await AccountTransaction.BeginAsync(db, account.NormalizedEmail!, ct);
        var order = await db.SkinPurchases.SingleAsync(x => x.Id == found.Id && x.UserId == found.UserId, ct);
        if (order.Status == "PAID") return order; // A webhook may have won the account lock.
        var references = payment.Transactions.Select(x => x.Reference).ToArray();
        if (await db.SkinPurchases.AnyAsync(x => x.Id != order.Id && x.ReceiptReference != null && references.Contains(x.ReceiptReference), ct))
            throw Error("PAYMENT_RECEIPT_REUSED", "Giao dịch đã được xử lý.");
        await GrantPaidAsync(order, payment.Transactions[^1].Reference, ct);
        await tx.CommitAsync(ct);
        return order;
    }
    public async Task<SkinPurchaseView> CreateAsync(Guid uid, SkinCheckoutInput input, CancellationToken ct)
    {
        if (!Available) throw Error("PAYMENTS_NOT_CONFIGURED", "Thanh toán chưa sẵn sàng. Chủ hệ thống cần cấu hình kênh payOS.", 503);
        if (input.Code != "fox" || input.RequestId == Guid.Empty) throw Error("INVALID_PURCHASE", "Chọn skin MILO và mã yêu cầu hợp lệ.", 400);
        // Reconcile a completed purchase before offering another paid checkout.
        await LatestAsync(uid, ct);
        var account = await db.Users.AsNoTracking().SingleAsync(x => x.Id == uid, ct);
        if (!account.IsActive || !account.EmailConfirmed) throw Error("FORBIDDEN", "Tài khoản không khả dụng.", 403);
        SkinPurchase order;
        var newlyReserved = false;
        await using (var tx = await AccountTransaction.BeginAsync(db, account.NormalizedEmail!, ct))
        {
            if (await db.OwnedPetSkins.AnyAsync(x => x.UserId == uid && x.SkinCode == "fox", ct))
                throw Error("SKIN_ALREADY_OWNED", "Bạn đã sở hữu skin MILO, không cần mua lại.");
            var existing = await db.SkinPurchases.SingleOrDefaultAsync(x => x.UserId == uid && x.RequestId == input.RequestId, ct);
            existing ??= await db.SkinPurchases.Where(x => x.UserId == uid && x.SkinCode == "fox" && x.Status != "PAID" && x.ExpiresAt > clock.GetUtcNow())
                .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            order = existing ?? new SkinPurchase { UserId = uid, RequestId = input.RequestId, CreatedAt = clock.GetUtcNow(),
                ExpiresAt = clock.GetUtcNow().AddMinutes(15), OrderCode = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8)) & 0x000FFFFFFFFFFFFF };
            if (order.OrderCode == 0) order.OrderCode = 1;
            if (existing is null) { db.SkinPurchases.Add(order); newlyReserved = true; }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        if (order.CheckoutUrl is not null || order.ExpiresAt <= clock.GetUtcNow()) return View(order);
        try
        {
            // Lookup first: a timed-out checkout must reuse its original orderCode.
            var link = (newlyReserved ? null : await gateway.FindAsync(order.OrderCode, ct)) ?? await gateway.CreateAsync(order.OrderCode, order.AmountVnd, order.ExpiresAt, ct);
            if (link.OrderCode != order.OrderCode || link.AmountVnd != 10000 || link.Currency != "VND" || string.IsNullOrWhiteSpace(link.Id) || link.Id.Length > 100 ||
                !Uri.TryCreate(link.CheckoutUrl, UriKind.Absolute, out var checkout) || checkout.Scheme != "https" || checkout.Host != "pay.payos.vn" || checkout.UserInfo.Length > 0 || !checkout.IsDefaultPort)
                throw Error("PAYMENT_LINK_INVALID", "Liên kết thanh toán không khớp đơn hàng.", 502);
            // GET does not return accountNumber. Recover it from the configured
            // channel on the signed webhook only when the lookup was authenticated.
            db.ChangeTracker.Clear();
            await using var tx = await AccountTransaction.BeginAsync(db, account.NormalizedEmail!, ct);
            order = await db.SkinPurchases.SingleAsync(x => x.Id == order.Id, ct);
            if (order.Status == "PAID") return View(order);
            order.PaymentLinkId = link.Id; order.CheckoutUrl = checkout.AbsoluteUri;
            order.AccountNumber = string.IsNullOrEmpty(link.AccountNumber) ? null : link.AccountNumber;
            order.Status = "PENDING"; order.Version = Guid.NewGuid();
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return View(order);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw Error("PAYMENT_PROVIDER_UNAVAILABLE", "Chưa xác nhận được liên kết payOS. Thử lại sẽ dùng cùng đơn hàng.", 503); }
    }
    public async Task HandleWebhookAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (!Available) throw Error("PAYMENTS_NOT_CONFIGURED", "Thanh toán chưa sẵn sàng.", 503);
        var receipt = PayOsPaymentSignatures.VerifyWebhook(body, options.Value.ChecksumKey)
            ?? throw Error("PAYMENT_SIGNATURE_INVALID", "Chữ ký payOS không hợp lệ.", 400);
        if (!receipt.Succeeded) return;
        var found = await db.SkinPurchases.AsNoTracking().SingleOrDefaultAsync(x => x.OrderCode == receipt.OrderCode, ct);
        // payOS sends a signed sample when registering the webhook. Unknown
        // orders are acknowledged without granting any skin.
        if (found is null) return;
        if (receipt.AmountVnd != found.AmountVnd || receipt.Currency != "VND" || found.SkinCode != "fox" || found.PaymentLinkId != receipt.PaymentLinkId)
            throw Error("PAYMENT_MISMATCH", "Giao dịch không khớp đơn hàng.");
        if (found.AccountNumber is { } checkoutAccount && checkoutAccount != receipt.AccountNumber)
        {
            // Never equate a QR virtual alias with the real settlement account.
            // Require an independently signed PAID lookup before accepting this
            // webhook, instead of dropping the bank check without verification.
            if ((await ReconcileAsync(found, ct)).Status == "PAID") return;
            throw Error("PAYMENT_MISMATCH", "Chưa đối soát được tài khoản nhận tiền với payOS.");
        }
        var account = await db.Users.AsNoTracking().SingleAsync(x => x.Id == found.UserId, ct);
        await using var tx = await AccountTransaction.BeginAsync(db, account.NormalizedEmail!, ct);
        var order = await db.SkinPurchases.SingleAsync(x => x.Id == found.Id, ct);
        if (receipt.AmountVnd != order.AmountVnd || receipt.Currency != "VND" || order.SkinCode != "fox" ||
            order.PaymentLinkId != receipt.PaymentLinkId || order.AccountNumber is { } bank && bank != receipt.AccountNumber)
            throw Error("PAYMENT_MISMATCH", "Giao dịch không khớp đơn hàng.");
        await GrantPaidAsync(order, receipt.Reference, ct);
        await tx.CommitAsync(ct);
    }
    // Both verified webhook and signed merchant lookup share the same atomic,
    // idempotent grant. Callers hold the account transaction lock.
    private async Task GrantPaidAsync(SkinPurchase order, string reference, CancellationToken ct)
    {
        if (order.Status == "PAID")
        {
            if (order.ReceiptReference != reference) throw Error("PAYMENT_MISMATCH", "Đơn hàng đã ghi nhận một giao dịch khác.");
            return;
        }
        if (await db.SkinPurchases.AnyAsync(x => x.Id != order.Id && x.ReceiptReference == reference, ct))
            throw Error("PAYMENT_RECEIPT_REUSED", "Giao dịch đã được xử lý.");
        order.Status = "PAID"; order.PaidAt = clock.GetUtcNow(); order.ReceiptReference = reference; order.Version = Guid.NewGuid();
        if (!await db.OwnedPetSkins.AnyAsync(x => x.UserId == order.UserId && x.SkinCode == order.SkinCode, ct))
            db.OwnedPetSkins.Add(new() { UserId = order.UserId, SkinCode = order.SkinCode, PurchaseId = order.Id, AcquiredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }
}
