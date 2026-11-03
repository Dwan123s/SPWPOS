using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SPWPOS.Application.Authentication;
namespace SPWPOS.Infrastructure.Commerce;

public sealed class PayOsGateway(HttpClient http, IOptions<PayOsOptions> options) : IPayOsGateway
{
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, "https://api-merchant.payos.vn" + path);
        request.Headers.Add("x-client-id", options.Value.ClientId);
        request.Headers.Add("x-api-key", options.Value.ApiKey);
        return request;
    }
    public async Task<PayOsLink?> FindAsync(long orderCode, CancellationToken ct)
    {
        var payment = await GetPaymentAsync(orderCode, ct);
        return payment is null ? null : new(payment.OrderCode, payment.AmountVnd, "VND", payment.Id,
            "https://pay.payos.vn/web/" + Uri.EscapeDataString(payment.Id), "");
    }
    public async Task<PayOsPaymentState?> GetPaymentAsync(long orderCode, CancellationToken ct)
    {
        try
        {
            using var request = Request(HttpMethod.Get, "/v2/payment-requests/" + orderCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            using var json = await ReadAsync(response, ct);
            CheckSuccess(json);
            return PayOsPaymentSignatures.VerifyPaymentInformation(json.RootElement, options.Value.ChecksumKey)
                ?? throw new AuthException("PAYMENT_SIGNATURE_INVALID", "Không xác minh được trạng thái từ payOS. Hãy thử kiểm tra lại.", 502);
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is HttpRequestException or TaskCanceledException)
        { throw new AuthException("PAYMENT_PROVIDER_UNAVAILABLE", "Chưa đối soát được với payOS. Không cần thanh toán lại; hãy kiểm tra lại sau.", 503); }
    }
    public async Task<PayOsLink> CreateAsync(long orderCode, int amount, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var config = options.Value;
        var cancel = new Uri(new Uri(config.PublicBaseUrl), "/?payment=cancelled");
        var success = new Uri(config.CheckoutReturnUrl);
        var description = "ML" + (orderCode % 10000000).ToString("D7", System.Globalization.CultureInfo.InvariantCulture);
        using var request = Request(HttpMethod.Post, "/v2/payment-requests");
        request.Content = JsonContent.Create(new { orderCode, amount, description,
            items = new[] { new { name = "MILO fox skin", quantity = 1, price = amount } },
            cancelUrl = cancel.AbsoluteUri, returnUrl = success.AbsoluteUri, expiredAt = expiresAt.ToUnixTimeSeconds(),
            signature = PayOsPaymentSignatures.SignCheckout(orderCode, amount, description, cancel, success, config.ChecksumKey) });
        using var response = await http.SendAsync(request, ct);
        using var json = await ReadAsync(response, ct); CheckSuccess(json);
        var root = json.RootElement; var data = root.GetProperty("data");
        if (!PayOsPaymentSignatures.VerifyData(data, root.GetProperty("signature").GetString(), config.ChecksumKey))
            throw new AuthException("PAYMENT_SIGNATURE_INVALID", "Không xác minh được phản hồi payOS. Hãy thử kiểm tra lại đơn hàng.", 502);
        return new(data.GetProperty("orderCode").GetInt64(), data.GetProperty("amount").GetInt32(), data.GetProperty("currency").GetString()!,
            data.GetProperty("paymentLinkId").GetString()!, data.GetProperty("checkoutUrl").GetString()!, data.GetProperty("accountNumber").GetString()!);
    }
    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode) throw new AuthException("PAYMENT_PROVIDER_UNAVAILABLE", "Chưa kết nối được payOS. Đơn hàng được giữ để thử lại.", 503);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    private static void CheckSuccess(JsonDocument json)
    {
        if (json.RootElement.GetProperty("code").GetString() != "00")
            throw new AuthException("PAYMENT_PROVIDER_REJECTED", "payOS chưa tạo được liên kết. Kiểm tra cấu hình kênh thanh toán hoặc thử lại đơn hàng.", 502);
    }
}
