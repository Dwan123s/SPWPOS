using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Commerce;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class CommerceAdminApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact] public async Task Only_admin_can_read_commerce_reporting_and_private_payment_fields_are_not_exposed()
    {
        await using var f=new ApiFactory(); using var client=await f.ClientAsync();
        foreach(var path in new[]{"revenue","purchases"}) { using var response=await client.GetAsync("/api/admin/commerce/"+path,Ct); Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode); }
        await Authorize(f,client);
        foreach(var path in new[]{"revenue","purchases"}) await Send(client,HttpMethod.Get,"/api/admin/commerce/"+path,expected:HttpStatusCode.Forbidden);
        await using(var scope=f.Services.CreateAsyncScope()) {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            Assert.True((await roles.CreateAsync(new("Admin"))).Succeeded);
            var user=(await users.FindByEmailAsync(ServiceHarness.Address))!;Assert.True((await users.AddToRoleAsync(user,"Admin")).Succeeded);
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.SkinPurchases.Add(new(){UserId=user.Id,RequestId=Guid.NewGuid(),OrderCode=321,Status="PAID",CreatedAt=f.Clock.GetUtcNow(),PaidAt=f.Clock.GetUtcNow(),ExpiresAt=f.Clock.GetUtcNow().AddMinutes(15),AmountVnd=10000,AccountNumber="private-bank-number",CheckoutUrl="https://pay.payos.vn/private",ReceiptReference="receipt-api"});
            await db.SaveChangesAsync(Ct);
        }
        var login=(await Send(client,HttpMethod.Post,"/api/auth/login",new{email=ServiceHarness.Address,password=ServiceHarness.Password})).GetProperty("data");
        client.DefaultRequestHeaders.Authorization=new("Bearer",login.GetProperty("accessToken").GetString());
        var report=(await Send(client,HttpMethod.Get,"/api/admin/commerce/revenue")).GetProperty("data");
        Assert.Equal(10000,report.GetProperty("selected").GetProperty("amountVnd").GetInt64());
        var order=(await Send(client,HttpMethod.Get,"/api/admin/commerce/purchases?status=PAID&search=321")).GetProperty("data").GetProperty("items")[0];
        Assert.Equal("receipt-api",order.GetProperty("receiptReference").GetString());
        Assert.False(order.TryGetProperty("accountNumber",out _));Assert.False(order.TryGetProperty("checkoutUrl",out _));
        await Send(client,HttpMethod.Get,"/api/admin/commerce/revenue?from=2026-11-03&to=2026-11-01",expected:HttpStatusCode.BadRequest);
        await Send(client,HttpMethod.Get,"/api/admin/commerce/purchases?pageSize=101",expected:HttpStatusCode.BadRequest);
        await Send(client,HttpMethod.Get,"/api/admin/commerce/revenue?from=invalid",expected:HttpStatusCode.BadRequest);
    }
    public static bool SqlServerAvailable=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    [Fact(SkipUnless=nameof(SqlServerAvailable),Skip="Requires SPWPOS_TEST_SQLSERVER."),Trait("Category","SqlServer")]
    public async Task SqlServer_executes_daily_aggregation_and_history_with_vietnam_date_boundaries()
    {
        await using var f=new ApiFactory(true);using var client=await f.ClientAsync();
        await using var scope=f.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var uid=Guid.NewGuid();
        db.Users.Add(new(){Id=uid,UserName="sql.commerce",NormalizedUserName="SQL.COMMERCE",Email="commerce@example.com",NormalizedEmail="COMMERCE@EXAMPLE.COM",FirstName="SQL",LastName="Buyer"});
        db.SkinPurchases.AddRange(new SkinPurchase{UserId=uid,RequestId=Guid.NewGuid(),OrderCode=1,Status="PAID",CreatedAt=DateTimeOffset.Parse("2026-11-01T00:00:00Z"),PaidAt=DateTimeOffset.Parse("2026-11-02T16:59:59Z"),ExpiresAt=DateTimeOffset.Parse("2026-11-01T01:00:00Z")},
            new SkinPurchase{UserId=uid,RequestId=Guid.NewGuid(),OrderCode=2,Status="PAID",CreatedAt=DateTimeOffset.Parse("2026-11-01T00:00:00Z"),PaidAt=DateTimeOffset.Parse("2026-11-02T17:00:00Z"),ExpiresAt=DateTimeOffset.Parse("2026-11-01T01:00:00Z")});
        await db.SaveChangesAsync(Ct);var service=scope.ServiceProvider.GetRequiredService<CommerceAdminService>();
        var report=await service.RevenueAsync(new(2026,11,2),new(2026,11,3),"day",Ct);
        Assert.Equal(2,report.Points.Length);Assert.All(report.Points,p=>Assert.Equal(10000,p.AmountVnd));
        var history=await service.PurchasesAsync(new(2026,11,3),new(2026,11,3),"PAID","commerce@example.com",1,20,Ct);
        Assert.Equal(2,Assert.Single(history.Items).OrderCode);
    }
}
