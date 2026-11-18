using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Commerce;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class TeamPlanPaymentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Gateway : IPayOsGateway
    {
        public PayOsLink? Link;
        public PayOsPaymentState? Payment;
        public bool TimeoutOnce;
        public int Creates;
        public Task<PayOsLink?> FindAsync(long code, CancellationToken ct) => Task.FromResult(Link);
        public Task<PayOsPaymentState?> GetPaymentAsync(long code, CancellationToken ct) => Task.FromResult(Payment);
        public Task<PayOsLink> CreateAsync(long code, int amount, DateTimeOffset expiresAt, CancellationToken ct, PayOsPremiumProduct? premiumProduct = null)
        {
            Assert.NotNull(premiumProduct);
            Creates++;
            Link = new(code, amount, "VND", "team-link-" + Creates, "https://pay.payos.vn/web/team-link-" + Creates, "bank");
            if (TimeoutOnce) { TimeoutOnce = false; throw new HttpRequestException("Synthetic timeout"); }
            return Task.FromResult(Link);
        }
    }

    private static async Task<T> Billing<T>(ServiceHarness h, Func<TeamPlanPaymentService, Task<T>> action)
    {
        await using var scope = h.Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TeamPlanPaymentService>());
    }

    [Theory]
    [InlineData("premium20", 50000, 20)]
    [InlineData("premiumUnlimited", 110000, null)]
    public async Task Only_verified_full_payment_unlocks_the_selected_team_plan(string code, int amount, int? limit)
    {
        var gateway = new Gateway();
        await using var h = await ServiceHarness.CreateAsync(services =>
        {
            services.AddSingleton<IPayOsGateway>(gateway);
            services.Configure<PayOsOptions>(x => { x.Enabled = true; x.ClientId = "test"; x.ApiKey = "test"; x.ChecksumKey = "test"; x.PublicBaseUrl = "https://planora.example/"; });
        });
        var user = await h.RegisterAsync();
        var team = new TeamProject { CreatorId = user.Id, Name = "Billing test" };
        await h.DbAsync(async db =>
        {
            db.TeamProjects.Add(team);
            db.TeamMemberships.Add(new() { TeamId = team.Id, UserId = user.Id, Role = "Leader" });
            await db.SaveChangesAsync(Ct);
        });
        var input = new TeamPlanCheckoutInput(code);
        gateway.TimeoutOnce = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => Billing(h, s => s.CreateAsync(user.Id, input, Ct)));
        var order = await Billing(h, s => s.CreateAsync(user.Id, input, Ct));
        Assert.Equal("PENDING", order.Status);
        Assert.Equal(1, gateway.Creates);
        Assert.Equal(amount, gateway.Link!.AmountVnd);
        Assert.Equal(1, await h.DbAsync(db => db.TeamPlanPurchases.CountAsync(Ct)));
        Assert.Null(await h.DbAsync(db => db.TeamPlanPurchases.Select(x => x.TeamId).SingleAsync(Ct)));
        Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => Billing(h, s => s.GetAsync(Guid.NewGuid(), team.Id, Ct)))).Code);
        gateway.Payment = new(gateway.Link.OrderCode, amount, gateway.Link.Id, "PAID", amount - 1, 1, [new(amount - 1, "team-receipt", "bank")]);
        Assert.Equal("PAYMENT_MISMATCH", (await Assert.ThrowsAsync<AuthException>(() => Billing(h, s => s.GetAsync(user.Id, team.Id, Ct)))).Code);
        Assert.False(await h.DbAsync(db => db.TeamPlanPurchases.AnyAsync(x => x.PaidAt != null, Ct)));
        await h.DbAsync(async db => { (await db.TeamPlanPurchases.SingleAsync(Ct)).ExpiresAt = h.Clock.GetUtcNow().AddMinutes(-1); await db.SaveChangesAsync(Ct); });
        gateway.Payment = new(gateway.Link.OrderCode, amount, gateway.Link.Id, "PAID", amount, 0, [new(amount, "team-receipt", "bank")]);
        Assert.Equal("PAID", (await Billing(h, s => s.GetAsync(user.Id, team.Id, Ct))).Order!.Status);
        Assert.Equal("PAID", (await Billing(h, s => s.GetAsync(user.Id, team.Id, Ct))).Order!.Status);
        await using var scope = h.Provider.CreateAsyncScope();
        var detail = await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().GetTeamAsync(user.Id, team.Id, Ct);
        Assert.Equal(code, detail.Plan!.Code);
        Assert.Equal(limit, detail.Plan.MemberLimit);
        var expires = h.Clock.GetUtcNow().AddMonths(1);
        Assert.Equal(expires, (await Billing(h, s => s.GetAsync(user.Id, Ct))).ValidUntil);
        h.Clock.Advance(TimeSpan.FromDays(10));
        gateway.Link = null;
        gateway.Payment = null;
        await Billing(h, s => s.CreateAsync(user.Id, input, Ct));
        gateway.Payment = new(gateway.Link!.OrderCode, amount, gateway.Link.Id, "PAID", amount, 0, [new(amount, "renewal-receipt", "bank")]);
        Assert.Equal(expires.AddMonths(1), (await Billing(h, s => s.GetAsync(user.Id, Ct))).ValidUntil);
        h.Clock.Advance(expires.AddMonths(1) - h.Clock.GetUtcNow());
        Assert.Equal("free", (await Billing(h, s => s.GetAsync(user.Id, Ct))).Plan.Code);
        Assert.Equal("free", (await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().GetTeamAsync(user.Id, team.Id, Ct)).Plan!.Code);
    }
}
