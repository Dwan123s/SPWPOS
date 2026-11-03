using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Commerce;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class PetCommerceTests
{
    private const string Key = "fictional-test-only-checksum";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Gateway : IPayOsGateway
    {
        public int Creates; public bool TimeoutOnce; public PayOsLink? Link;
        public PayOsPaymentState? Payment;
        public Task<PayOsPaymentState?> GetPaymentAsync(long code, CancellationToken ct) => Task.FromResult(Payment);
        public Task<PayOsLink?> FindAsync(long code, CancellationToken ct) => Task.FromResult(Link);
        public Task<PayOsLink> CreateAsync(long code, int amount, DateTimeOffset expiresAt, CancellationToken ct)
        {
            Creates++; Link = new(code, amount, "VND", "link-1", "https://pay.payos.vn/web/link-1", "123456");
            if (TimeoutOnce) { TimeoutOnce = false; throw new HttpRequestException("synthetic timeout"); }
            return Task.FromResult(Link);
        }
    }
    private static async Task<ServiceHarness> Harness(Gateway gateway) => await ServiceHarness.CreateAsync(services => {
        services.AddSingleton<IPayOsGateway>(gateway);
        services.Configure<PayOsOptions>(x => { x.Enabled = true; x.ClientId = "fictional-client"; x.ApiKey = "fictional-api"; x.ChecksumKey = Key; x.PublicBaseUrl = "https://planora.example/"; });
    });
    private static async Task<T> Payment<T>(ServiceHarness h, Func<SkinPaymentService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<SkinPaymentService>()); }
    private static Task Webhook(ServiceHarness h, byte[] body) => Payment(h, async s => { await s.HandleWebhookAsync(body, Ct); return true; });
    private static byte[] Receipt(long order, int amount = 10000, string bank = "123456", string link = "link-1", string reference = "receipt-1", string code = "00")
    {
        var data = new { orderCode = order, amount, currency = "VND", accountNumber = bank, paymentLinkId = link, reference, code };
        var element = JsonSerializer.SerializeToElement(data);
        var canonical = string.Join('&', element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Name + "=" + x.Value.ToString()));
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return JsonSerializer.SerializeToUtf8Bytes(new { success = true, data, signature });
    }
    [Fact] public async Task Paid_skin_requires_exact_verified_receipt_and_duplicate_webhook_grants_once()
    {
        var gateway = new Gateway(); await using var h = await Harness(gateway); var user = await h.RegisterAsync();
        var input = new SkinCheckoutInput("fox", Guid.NewGuid());
        var purchase = await Payment(h, s => s.CreateAsync(user.Id, input, Ct));
        var repeat = await Payment(h, s => s.CreateAsync(user.Id, input, Ct));
        Assert.Equal(purchase.Id, repeat.Id); Assert.Equal(1, gateway.Creates); Assert.Equal(10000, purchase.AmountVnd);
        Assert.Equal("https://planora.example/?payment=returned", purchase.ReturnUrl);
        Assert.Equal(purchase.ReturnUrl, repeat.ReturnUrl);
        Assert.Equal(0, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
        var code = gateway.Link!.OrderCode;
        foreach (var invalid in new[] { Receipt(code, amount: 1), Receipt(code, bank: "other"), Receipt(code, link: "other") })
            Assert.Equal("PAYMENT_MISMATCH", (await Assert.ThrowsAsync<AuthException>(() => Webhook(h, invalid))).Code);
        Assert.Equal("PAYMENT_SIGNATURE_INVALID", (await Assert.ThrowsAsync<AuthException>(() => Webhook(h, Encoding.UTF8.GetBytes("{}")))).Code);
        await Webhook(h, Receipt(code, code: "01"));
        Assert.Equal(0, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
        await Webhook(h, Receipt(123)); // Signed provider setup sample never grants.
        await Webhook(h, Receipt(code)); await Webhook(h, Receipt(code));
        Assert.Equal(1, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
        Assert.Equal("PAID", (await Payment(h, s => s.GetAsync(user.Id, purchase.Id, Ct))).Status);
        Assert.Equal("SKIN_ALREADY_OWNED", (await Assert.ThrowsAsync<AuthException>(() => Payment(h, s => s.CreateAsync(user.Id, new("fox", Guid.NewGuid()), Ct)))).Code);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Payment(h, s => s.GetAsync(Guid.NewGuid(), purchase.Id, Ct)))).Code);
        await using var scope = h.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await service.SelectSkinAsync(user.Id, new("fox"), Ct);
        Assert.Equal("fox", (await service.GetGamificationAsync(user.Id, Ct)).SelectedSkin);
        Assert.Equal(0, (await service.GetGamificationAsync(user.Id, Ct)).ExperiencePoints);
    }
    [Fact] public async Task Provider_timeout_reuses_persisted_order_code_and_recovers_checkout()
    {
        var gateway = new Gateway { TimeoutOnce = true }; await using var h = await Harness(gateway); var user = await h.RegisterAsync();
        var input = new SkinCheckoutInput("fox", Guid.NewGuid());
        await Assert.ThrowsAsync<AuthException>(() => Payment(h, s => s.CreateAsync(user.Id, input, Ct)));
        var purchase = await Payment(h, s => s.CreateAsync(user.Id, input, Ct));
        Assert.Equal("PENDING", purchase.Status); Assert.Equal(1, gateway.Creates);
        Assert.Equal(1, await h.DbAsync(db => db.SkinPurchases.CountAsync(Ct)));
    }
    [Fact] public async Task Signed_lookup_recovers_a_missed_webhook_even_after_local_expiry_and_late_webhook_is_idempotent()
    {
        var gateway = new Gateway(); await using var h = await Harness(gateway); var user = await h.RegisterAsync();
        var purchase = await Payment(h, s => s.CreateAsync(user.Id, new("fox", Guid.NewGuid()), Ct));
        await h.DbAsync(async db => { (await db.SkinPurchases.SingleAsync(Ct)).ExpiresAt = h.Clock.GetUtcNow().AddMinutes(-1); await db.SaveChangesAsync(Ct); });
        gateway.Payment = new(gateway.Link!.OrderCode, 10000, "link-1", "PAID", 10000, 0, [new(10000, "receipt-1", "123456")]);
        var updated = await Payment(h, s => s.GetAsync(user.Id, purchase.Id, Ct));
        Assert.Equal("PAID", updated.Status);
        Assert.Equal("PAID", (await Payment(h, s => s.LatestAsync(user.Id, Ct)))!.Status);
        await Webhook(h, Receipt(gateway.Link.OrderCode));
        await Webhook(h, Receipt(gateway.Link.OrderCode));
        Assert.Equal(1, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
        Assert.Equal("receipt-1", await h.DbAsync(db => db.SkinPurchases.Select(x => x.ReceiptReference).SingleAsync(Ct)));
    }
    [Theory] [InlineData("id")] [InlineData("order")] [InlineData("amount")] [InlineData("underpaid")]
    [InlineData("remaining")] [InlineData("sum")] [InlineData("empty-bank")] [InlineData("duplicate")]
    public async Task Reconciliation_never_grants_for_mismatched_or_incomplete_provider_evidence(string invalid)
    {
        var gateway = new Gateway(); await using var h = await Harness(gateway); var user = await h.RegisterAsync();
        var purchase = await Payment(h, s => s.CreateAsync(user.Id, new("fox", Guid.NewGuid()), Ct));
        gateway.Payment = new(gateway.Link!.OrderCode, 10000, "link-1", "PAID", 10000, 0, [new(10000, "receipt-1", "123456")]);
        gateway.Payment = invalid switch {
            "id" => gateway.Payment with { Id = "other" },
            "order" => gateway.Payment with { OrderCode = gateway.Payment.OrderCode + 1 },
            "amount" => gateway.Payment with { AmountVnd = 1 },
            "underpaid" => gateway.Payment with { AmountPaid = 9000 },
            "remaining" => gateway.Payment with { AmountRemaining = 1 },
            "sum" => gateway.Payment with { Transactions = [new(9999, "receipt-1", "123456")] },
            "empty-bank" => gateway.Payment with { Transactions = [new(10000, "receipt-1", "")] },
            _ => gateway.Payment with { Transactions = [new(5000, "receipt-1", "123456"), new(5000, "receipt-1", "123456")] }
        };
        Assert.Equal("PAYMENT_MISMATCH", (await Assert.ThrowsAsync<AuthException>(() => Payment(h, s => s.GetAsync(user.Id, purchase.Id, Ct)))).Code);
        Assert.Equal(0, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
        Assert.Equal("PENDING", await h.DbAsync(db => db.SkinPurchases.Select(x => x.Status).SingleAsync(Ct)));
    }
    [Theory] [InlineData("PENDING")] [InlineData("UNDERPAID")] [InlineData("CANCELLED")] [InlineData("EXPIRED")]
    public async Task Non_paid_provider_statuses_never_unlock_a_skin(string status)
    {
        var gateway = new Gateway(); await using var h = await Harness(gateway); var user = await h.RegisterAsync();
        var purchase = await Payment(h, s => s.CreateAsync(user.Id, new("fox", Guid.NewGuid()), Ct));
        gateway.Payment = new(gateway.Link!.OrderCode, 10000, "link-1", status, 0, 10000, []);
        Assert.Equal("PENDING", (await Payment(h, s => s.GetAsync(user.Id, purchase.Id, Ct))).Status);
        Assert.Equal(0, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
    }
    [Fact] public async Task Reconciles_before_creating_another_purchase_and_checks_order_ownership()
    {
        var gateway = new Gateway(); await using var h = await Harness(gateway); var user = await h.RegisterAsync();
        var purchase = await Payment(h, s => s.CreateAsync(user.Id, new("fox", Guid.NewGuid()), Ct));
        gateway.Payment = new(gateway.Link!.OrderCode, 10000, "link-1", "PAID", 10000, 0, [new(10000, "receipt-1", "123456")]);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Payment(h, s => s.GetAsync(Guid.NewGuid(), purchase.Id, Ct)))).Code);
        Assert.Equal(0, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
        Assert.Equal("SKIN_ALREADY_OWNED", (await Assert.ThrowsAsync<AuthException>(() => Payment(h, s => s.CreateAsync(user.Id, new("fox", Guid.NewGuid()), Ct)))).Code);
        Assert.Equal(1, gateway.Creates);
        Assert.Equal(1, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
    }
    [Fact] public async Task Virtual_checkout_alias_can_differ_from_real_account_but_requires_signed_paid_lookup()
    {
        var gateway = new Gateway(); await using var h = await Harness(gateway); var user = await h.RegisterAsync();
        var purchase = await Payment(h, s => s.CreateAsync(user.Id, new("fox", Guid.NewGuid()), Ct));
        await h.DbAsync(async db => { (await db.SkinPurchases.SingleAsync(Ct)).AccountNumber = "VQRVIRTUAL123"; await db.SaveChangesAsync(Ct); });
        var receipt = Receipt(gateway.Link!.OrderCode, bank: "987654321");
        Assert.Equal("PAYMENT_MISMATCH", (await Assert.ThrowsAsync<AuthException>(() => Webhook(h, receipt))).Code);
        Assert.Equal(0, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
        gateway.Payment = new(gateway.Link.OrderCode, 10000, "link-1", "PAID", 10000, 0, [new(10000, "receipt-1", "987654321")]);
        await Webhook(h, receipt);
        await Webhook(h, receipt);
        Assert.Equal("PAID", (await Payment(h, s => s.GetAsync(user.Id, purchase.Id, Ct))).Status);
        Assert.Equal(1, await h.DbAsync(db => db.OwnedPetSkins.CountAsync(Ct)));
    }
    [Theory] [InlineData(0,1,1,5)] [InlineData(399,4,1,5)] [InlineData(400,5,2,10)] [InlineData(899,9,2,10)] [InlineData(900,10,3,null)]
    public async Task Evolution_uses_xp_thresholds_and_money_skin_cannot_unlock_with_xp(long xp, int level, int stage, int? next)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        await h.DbAsync(async db => { (await db.Users.SingleAsync(Ct)).ExperiencePoints = xp; await db.SaveChangesAsync(Ct); });
        await using var scope = h.Provider.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        var profile = await service.GetGamificationAsync(user.Id, Ct);
        Assert.Equal(level, profile.UserLevel); Assert.Equal(stage, profile.EvolutionStage); Assert.Equal(next, profile.NextEvolutionLevel);
        Assert.Equal("classic", profile.SelectedSkin); Assert.False(profile.Inventory.Single(s => s.Code == "fox").Unlocked);
        Assert.Equal(10000, profile.Inventory.Single(s => s.Code == "fox").PriceVnd);
        Assert.Equal("SKIN_NOT_OWNED", (await Assert.ThrowsAsync<AuthException>(() => service.SelectSkinAsync(user.Id, new("fox"), Ct))).Code);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Task_reward_is_on_time_only_and_not_repeated_after_reopening(bool late)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var task = new SPWPOS.Domain.Entities.WorkTask { UserId = user.Id, Name = "Task XP", Deadline = h.Clock.GetUtcNow().AddHours(late ? -1 : 1) };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); await db.SaveChangesAsync(Ct); });
        await using var scope = h.Provider.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await service.SetStatusAsync(user.Id, task.Id, new("Completed",100), Ct);
        await service.SetStatusAsync(user.Id, task.Id, new("Todo",0), Ct);
        await service.SetStatusAsync(user.Id, task.Id, new("Completed",100), Ct);
        Assert.Equal(late ? 0 : 25, (await service.GetGamificationAsync(user.Id, Ct)).ExperiencePoints);
        Assert.Equal(!late, await h.DbAsync(db => db.WorkTasks.Select(t => t.TaskExperienceAwarded).SingleAsync(Ct)));
    }
}
