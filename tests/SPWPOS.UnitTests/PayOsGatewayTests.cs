using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
using SPWPOS.Infrastructure.Commerce;
namespace SPWPOS.UnitTests;

public sealed class PayOsGatewayTests
{
    private const string Key = "synthetic-merchant-checksum";
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
