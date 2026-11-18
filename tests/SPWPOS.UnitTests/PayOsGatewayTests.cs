using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure.Commerce;
namespace SPWPOS.UnitTests;

public sealed class PayOsGatewayTests
{
    private const string Key = "synthetic-merchant-checksum";
    private sealed class CheckoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var data = body.RootElement;
            Assert.Equal("PR0000123", data.GetProperty("description").GetString());
            Assert.Equal("https://planora.example/?premiumPayment=returned", data.GetProperty("returnUrl").GetString());
            Assert.Equal("https://planora.example/?premiumPayment=cancelled", data.GetProperty("cancelUrl").GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"code":"99","desc":"Synthetic rejection"}""") };
        }
    }

    [Fact]
    public async Task Premium_checkout_signs_a_short_description_and_returns_to_account_page()
    {
        using var client = new HttpClient(new CheckoutHandler());
        var gateway = new PayOsGateway(client, Options.Create(new PayOsOptions { ClientId = "test-client", ApiKey = "test-api", ChecksumKey = Key, PublicBaseUrl = "https://planora.example/" }));
        var error = await Assert.ThrowsAsync<AuthException>(() => gateway.CreateAsync(123, 50000, DateTimeOffset.UtcNow.AddMinutes(15), TestContext.Current.CancellationToken, new("Premium 20")));
        Assert.Equal("PAYMENT_PROVIDER_REJECTED", error.Code);
    }
    private sealed class Handler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api-merchant.payos.vn/v2/payment-requests/123", request.RequestUri!.AbsoluteUri);
            Assert.Equal("test-client", request.Headers.GetValues("x-client-id").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
    [Fact]
    public async Task Missing_payment_returned_as_provider_code_101_allows_checkout_creation()
    {
        using var client = new HttpClient(new Handler("""{"code":"101","desc":"Mã thanh toán không tồn tại","data":null}"""));
        var gateway = new PayOsGateway(client, Options.Create(new PayOsOptions { ClientId = "test-client", ApiKey = "test-api" }));
        Assert.Null(await gateway.GetPaymentAsync(123, TestContext.Current.CancellationToken));
        Assert.Null(await gateway.FindAsync(123, TestContext.Current.CancellationToken));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Reads_verified_payment_information_but_never_trusts_an_unsigned_paid_status(bool tamper)
    {
        const string canonical = """amount=10000&amountPaid=10000&amountRemaining=0&id=link-123&orderCode=123&status=PAID&transactions=[{"accountNumber":"123456","amount":10000,"reference":"receipt-1"}]""";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var body = """{"code":"00","data":{"id":"link-123","orderCode":123,"amount":10000,"amountPaid":10000,"amountRemaining":0,"status":"PAID","transactions":[{"reference":"receipt-1","amount":10000,"accountNumber":"123456"}]},"signature":"SIGNATURE"}""".Replace("SIGNATURE", signature);
        if (tamper) body = body.Replace("receipt-1", "forged-receipt");
        using var client = new HttpClient(new Handler(body));
        var gateway = new PayOsGateway(client, Options.Create(new PayOsOptions { ClientId = "test-client", ApiKey = "test-api", ChecksumKey = Key }));
        if (tamper)
            Assert.Equal("PAYMENT_SIGNATURE_INVALID", (await Assert.ThrowsAsync<AuthException>(() => gateway.GetPaymentAsync(123, TestContext.Current.CancellationToken))).Code);
        else {
            var state = await gateway.GetPaymentAsync(123, TestContext.Current.CancellationToken);
            Assert.NotNull(state); Assert.Equal("PAID", state.Status); Assert.Equal(10000, state.AmountPaid);
        }
    }
}
