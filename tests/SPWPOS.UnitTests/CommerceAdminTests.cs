using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Commerce;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class CommerceAdminTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Report<T>(ServiceHarness h, Func<CommerceAdminService, Task<T>> action)
    { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<CommerceAdminService>()); }
    private static SkinPurchase Order(Guid uid, long code, string created, string? paid = null, string status = "PAID", int amount = 10000) => new()
    {
        UserId = uid, RequestId = Guid.NewGuid(), OrderCode = code, AmountVnd = amount, CreatedAt = DateTimeOffset.Parse(created),
        ExpiresAt = DateTimeOffset.Parse(created).AddMinutes(15), Status = status, PaidAt = paid is null ? null : DateTimeOffset.Parse(paid), ReceiptReference = paid is null ? null : "receipt-" + code
    };
    [Fact] public async Task Revenue_uses_verified_paid_time_vietnam_midnight_and_monday_with_zero_days()
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync();
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-03T05:00:00Z") - h.Clock.GetUtcNow());
        await h.DbAsync(async db => {
            db.SkinPurchases.AddRange(
                Order(u.Id, 1, "2026-10-01T00:00:00Z", "2026-11-01T16:59:59Z"), // Sunday 23:59:59
                Order(u.Id, 2, "2026-10-01T00:00:00Z", "2026-11-01T17:00:00Z"), // Monday midnight
                Order(u.Id, 3, "2026-10-01T00:00:00Z", "2026-11-02T17:00:00Z"), // Tuesday midnight
                Order(u.Id, 4, "2026-11-03T00:00:00Z", status: "PENDING"),
                Order(u.Id, 5, "2026-11-03T00:00:00Z", status: "CANCELLED"),
                Order(u.Id, 6, "2026-11-03T00:00:00Z", status: "PAID")); // Missing paid evidence excluded
            await db.SaveChangesAsync(Ct);
        });
        var report = await Report(h, s => s.RevenueAsync(new(2026,11,1), new(2026,11,4), "day", Ct));
        Assert.Equal(new RevenueTotal(10000, 1), report.Today); Assert.Equal(new RevenueTotal(20000, 2), report.Week);
        Assert.Equal(new RevenueTotal(30000, 3), report.Month); Assert.Equal(report.Month, report.AllTime);
        Assert.Equal(report.Month, report.Selected); Assert.Equal(1, report.PayingCustomers);
        Assert.Equal(4, report.Points.Length); Assert.Equal(0, report.Points[^1].AmountVnd);
        var weeks = await Report(h, s => s.RevenueAsync(new(2026,11,1), new(2026,11,4), "week", Ct));
        Assert.Equal(2, weeks.Points.Length); Assert.Equal(10000, weeks.Points[0].AmountVnd); Assert.Equal(20000, weeks.Points[1].AmountVnd);
        Assert.Equal(new DateOnly(2026,11,2), weeks.Points[1].From);
    }
    [Fact] public async Task Monthly_buckets_clip_ranges_and_large_sums_never_overflow_int()
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync();
        await h.DbAsync(async db => { db.SkinPurchases.AddRange(
            Order(u.Id,1,"2026-10-30T00:00:00Z","2026-10-31T16:59:59Z", amount: 2000000000),
            Order(u.Id,2,"2026-10-30T00:00:00Z","2026-10-31T17:00:00Z", amount: 2000000000)); await db.SaveChangesAsync(Ct); });
        var r = await Report(h,s => s.RevenueAsync(new(2026,10,30),new(2026,11,3),"month",Ct));
        Assert.Equal(4000000000L,r.Selected.AmountVnd); Assert.Equal(2,r.Points.Length);
        Assert.Equal(new DateOnly(2026,10,30),r.Points[0].From); Assert.Equal(new DateOnly(2026,10,31),r.Points[0].To);
        Assert.Equal(new DateOnly(2026,11,3),r.Points[1].To);
    }
    [Fact] public async Task History_uses_paid_or_created_time_searches_and_pages_without_mutating_status()
    {
        await using var h = await ServiceHarness.CreateAsync(); var u = await h.RegisterAsync();
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-03T05:00:00Z") - h.Clock.GetUtcNow());
        await h.DbAsync(async db => { db.SkinPurchases.AddRange(
            Order(u.Id,10,"2026-10-01T00:00:00Z","2026-11-03T01:00:00Z"),
            Order(u.Id,11,"2026-11-03T01:00:00Z",status:"PENDING"),
            Order(u.Id,12,"2026-11-03T04:59:00Z",status:"PENDING"),
            Order(u.Id,13,"2026-11-03T01:00:00Z",status:"CANCELLED")); await db.SaveChangesAsync(Ct); });
        var expired = await Report(h,s => s.PurchasesAsync(new(2026,11,3),new(2026,11,3),"EXPIRED",null,1,20,Ct));
        Assert.Equal(11,Assert.Single(expired.Items).OrderCode);
        var pending = await Report(h,s => s.PurchasesAsync(new(2026,11,3),new(2026,11,3),"PENDING",null,1,20,Ct));
        Assert.Equal(12,Assert.Single(pending.Items).OrderCode);
        var search = await Report(h,s => s.PurchasesAsync(new(2026,11,3),new(2026,11,3),"PAID","receipt-10",1,20,Ct));
        Assert.Equal(10,Assert.Single(search.Items).OrderCode);
        var page1 = await Report(h,s => s.PurchasesAsync(new(2026,11,3),new(2026,11,3),null,ServiceHarness.Address,1,2,Ct));
        var page2 = await Report(h,s => s.PurchasesAsync(new(2026,11,3),new(2026,11,3),null,ServiceHarness.Address,2,2,Ct));
        Assert.Equal(4,page1.Total); Assert.Equal(2,page1.Items.Length); Assert.Equal(2,page2.Items.Length);
        Assert.Empty(page1.Items.Select(x=>x.Id).Intersect(page2.Items.Select(x=>x.Id)));
        Assert.Equal("PENDING",await h.DbAsync(db => db.SkinPurchases.Where(x=>x.OrderCode==11).Select(x=>x.Status).SingleAsync(Ct)));
    }
    [Fact] public async Task Empty_report_is_zero_and_invalid_filters_are_rejected()
    {
        await using var h = await ServiceHarness.CreateAsync();
        var r=await Report(h,s=>s.RevenueAsync(new(2026,11,1),new(2026,11,3),"day",Ct));
        Assert.Equal(new RevenueTotal(0,0),r.Selected); Assert.Equal(3,r.Points.Length); Assert.All(r.Points,p=>Assert.Equal(0,p.AmountVnd));
        foreach(var args in new[]{(new DateOnly(2026,11,3),new DateOnly(2026,11,1),"day"),(new DateOnly(2025,1,1),new DateOnly(2026,11,1),"day"),(new DateOnly(2026,11,1),new DateOnly(2026,11,3),"other")})
            Assert.Equal(400,(await Assert.ThrowsAsync<AuthException>(()=>Report(h,s=>s.RevenueAsync(args.Item1,args.Item2,args.Item3,Ct)))).StatusCode);
        await Assert.ThrowsAsync<AuthException>(()=>Report(h,s=>s.PurchasesAsync(null,null,"FAKE",null,1,20,Ct)));
        await Assert.ThrowsAsync<AuthException>(()=>Report(h,s=>s.PurchasesAsync(null,null,null,null,0,20,Ct)));
        await Assert.ThrowsAsync<AuthException>(()=>Report(h,s=>s.PurchasesAsync(null,null,null,null,1,101,Ct)));
    }
}
