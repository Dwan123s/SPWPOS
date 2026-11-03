using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Commerce;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class PetPaymentApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Key = "synthetic-integration-checksum";
    private sealed class Gateway : IPayOsGateway
    {
        public long OrderCode;
        public PayOsPaymentState? Payment;
        public Task<PayOsPaymentState?> GetPaymentAsync(long code, CancellationToken ct) => Task.FromResult(Payment);
        public Task<PayOsLink?> FindAsync(long code, CancellationToken ct) => Task.FromResult<PayOsLink?>(null);
        public Task<PayOsLink> CreateAsync(long code, int amount, DateTimeOffset expiresAt, CancellationToken ct)
        { OrderCode = code; return Task.FromResult(new PayOsLink(code,amount,"VND","link-api","https://pay.payos.vn/web/link-api","123456")); }
    }
    private sealed class Factory : ApiFactory
    {
        public Gateway Gateway { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => {
                services.AddSingleton<IPayOsGateway>(Gateway);
                services.Configure<PayOsOptions>(x => { x.Enabled=true; x.ClientId="test"; x.ApiKey="test"; x.ChecksumKey=Key; x.PublicBaseUrl="https://planora.example/"; });
            });
        }
    }
    [Fact] public async Task Checkout_needs_login_and_only_verified_webhook_unlocks_purchased_skin()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        using var anonymous = await client.PostAsync("/api/commerce/payments", new StringContent("{}",Encoding.UTF8,"application/json"),Ct);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        await Authorize(f,client);
        var caps=(await Send(client,HttpMethod.Get,"/api/commerce/payments/capabilities")).GetProperty("data");
        Assert.True(caps.GetProperty("available").GetBoolean());
        var order=(await Send(client,HttpMethod.Post,"/api/commerce/payments",new SkinCheckoutInput("fox",Guid.NewGuid()))).GetProperty("data");
        Assert.Equal(10000,order.GetProperty("amountVnd").GetInt32());
        Assert.Equal("https://planora.example/?payment=returned",order.GetProperty("returnUrl").GetString());
        await Send(client,HttpMethod.Post,"/api/workspace/gamification/skin",new SkinInput("fox"),HttpStatusCode.Conflict);
        using var webhookClient=await f.ClientAsync(); // no authorization header
        using var forged=await webhookClient.PostAsync("/api/commerce/payos/webhook",new StringContent("{}",Encoding.UTF8,"application/json"),Ct);
        Assert.Equal(HttpStatusCode.BadRequest,forged.StatusCode);
        var data=new { orderCode=f.Gateway.OrderCode,amount=10000,currency="VND",paymentLinkId="link-api",accountNumber="123456",reference="api-receipt",code="00" };
        var element=JsonSerializer.SerializeToElement(data);
        var canonical=string.Join('&',element.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>p.Name+"="+p.Value));
        var signature=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var json=JsonSerializer.Serialize(new { data,signature });
        for(var i=0;i<2;i++)
        {
            using var verified=await webhookClient.PostAsync("/api/commerce/payos/webhook",new StringContent(json,Encoding.UTF8,"application/json"),Ct);
            Assert.Equal(HttpStatusCode.OK,verified.StatusCode);
        }
        await Send(client,HttpMethod.Post,"/api/workspace/gamification/skin",new SkinInput("fox"));
        var profile=(await Send(client,HttpMethod.Get,"/api/workspace/gamification/profile")).GetProperty("data");
        Assert.Equal("fox",profile.GetProperty("selectedSkin").GetString());
        await using var scope=f.Services.CreateAsyncScope();
        Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OwnedPetSkins.CountAsync(Ct));
    }
    [Fact] public async Task Checkout_CSP_allows_official_embed_without_allowing_arbitrary_frames_or_inline_scripts()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        using var response = await client.GetAsync("/", Ct);
        var policy = string.Join(" ", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("https://cdn.payos.vn/payos-checkout/v1/stable/payos-initialize.js", policy);
        Assert.Contains("https://pay.payos.vn https://next.pay.payos.vn", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.DoesNotContain("'unsafe-inline'", policy);
        Assert.DoesNotContain("frame-src 'none' https", policy);
    }
    [Fact] public async Task Checking_a_purchase_recovers_missing_webhook_and_unlocks_inventory_once()
    {
        await using var f = new Factory(); using var client = await f.ClientAsync();
        await Authorize(f, client);
        var order = (await Send(client, HttpMethod.Post, "/api/commerce/payments", new SkinCheckoutInput("fox", Guid.NewGuid()))).GetProperty("data");
        f.Gateway.Payment = new(f.Gateway.OrderCode, 10000, "link-api", "PAID", 10000, 0, [new(10000, "reconciled-receipt", "123456")]);
        var check = (await Send(client, HttpMethod.Get, "/api/commerce/payments/" + order.GetProperty("id").GetString())).GetProperty("data");
        Assert.Equal("PAID", check.GetProperty("status").GetString());
        var latest = (await Send(client, HttpMethod.Get, "/api/commerce/payments/latest")).GetProperty("data");
        Assert.Equal("PAID", latest.GetProperty("status").GetString());
        await Send(client, HttpMethod.Post, "/api/workspace/gamification/skin", new SkinInput("fox"));
        await using var scope = f.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OwnedPetSkins.CountAsync(Ct));
    }
}
