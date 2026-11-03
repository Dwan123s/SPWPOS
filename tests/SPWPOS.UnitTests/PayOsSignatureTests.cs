using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPWPOS.Infrastructure.Commerce;
namespace SPWPOS.UnitTests;

public sealed class PayOsSignatureTests
{
    private const string SyntheticKey = "synthetic-offline-checksum-not-a-merchant-key";
    private static string Hmac(string input) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SyntheticKey), Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    [Fact] public void Checkout_uses_the_payment_request_canonical_string_without_url_encoding_values()
    {
        var cancel = new Uri("https://planora.example/payment/cancel?order=123&view=shop");
        var success = new Uri("https://planora.example/payment/return?order=123");
        var actual = PayOsPaymentSignatures.SignCheckout(123, 10000, "PN123", cancel, success, SyntheticKey);
        Assert.Equal(Hmac("amount=10000&cancelUrl=https://planora.example/payment/cancel?order=123&view=shop&description=PN123&orderCode=123&returnUrl=https://planora.example/payment/return?order=123"), actual);
        Assert.Throws<ArgumentException>(() => PayOsPaymentSignatures.SignCheckout(123,10000,"PN123",new("http://planora.example/cancel"),success,SyntheticKey));
    }
    [Fact] public void Webhook_checks_signed_data_and_rejects_amount_tampering_duplicate_fields_and_bad_hex()
    {
        const string canonical = "accountNumber=123456&amount=10000&code=00&currency=VND&orderCode=123&paymentLinkId=link-123&reference=receipt-1";
        var signature=Hmac(canonical);
        string Envelope(int amount) => JsonSerializer.Serialize(new { success=false, data=new { reference="receipt-1",currency="VND",amount,orderCode=123,accountNumber="123456",code="00",paymentLinkId="link-123" },signature });
        var verified=PayOsPaymentSignatures.VerifyWebhook(Encoding.UTF8.GetBytes(Envelope(10000)),SyntheticKey);
        Assert.NotNull(verified);Assert.True(verified.Succeeded);Assert.Equal(10000,verified.AmountVnd);
        Assert.Null(PayOsPaymentSignatures.VerifyWebhook(Encoding.UTF8.GetBytes(Envelope(1)),SyntheticKey));
        using var duplicates=JsonDocument.Parse("""{"amount":10000,"amount":1}""");
        Assert.False(PayOsPaymentSignatures.VerifyData(duplicates.RootElement,signature,SyntheticKey));
        using var value=JsonDocument.Parse("""{"amount":10000}""");
        Assert.False(PayOsPaymentSignatures.VerifyData(value.RootElement,new string('z',64),SyntheticKey));
    }
    [Fact] public void Null_canonicalization_and_unknown_nested_fields_fail_safely()
    {
        using var data=JsonDocument.Parse("""{"reference":"a&b=c","amount":10000,"empty":null}""");
        Assert.True(PayOsPaymentSignatures.VerifyData(data.RootElement,Hmac("amount=10000&empty=&reference=a&b=c"),SyntheticKey));
        using var nested=JsonDocument.Parse("""{"amount":10000,"newField":{"a":1}}""");
        Assert.False(PayOsPaymentSignatures.VerifyData(nested.RootElement,new string('0',64),SyntheticKey));
    }
    [Fact] public void Payment_information_verifies_sorted_transaction_json_and_rejects_tampering()
    {
        const string json = """{"status":"PAID","transactions":[{"reference":"receipt-1","amount":10000,"accountNumber":"123456","description":"Mua cáo MILO","counterAccountName":null}],"amountRemaining":0,"id":"link-123","amountPaid":10000,"amount":10000,"orderCode":123,"canceledAt":null}""";
        const string canonical = """amount=10000&amountPaid=10000&amountRemaining=0&canceledAt=&id=link-123&orderCode=123&status=PAID&transactions=[{"accountNumber":"123456","amount":10000,"counterAccountName":null,"description":"Mua cáo MILO","reference":"receipt-1"}]""";
        using var envelope = JsonDocument.Parse("{\"code\":\"00\",\"data\":" + json + ",\"signature\":\"" + Hmac(canonical) + "\"}");
        var payment = PayOsPaymentSignatures.VerifyPaymentInformation(envelope.RootElement, SyntheticKey);
        Assert.NotNull(payment); Assert.Equal(10000, payment.AmountPaid); Assert.Single(payment.Transactions);
        using var tampered = JsonDocument.Parse(envelope.RootElement.GetRawText().Replace("Mua cáo MILO", "Other"));
        Assert.Null(PayOsPaymentSignatures.VerifyPaymentInformation(tampered.RootElement, SyntheticKey));
        Assert.False(PayOsPaymentSignatures.VerifyData(envelope.RootElement.GetProperty("data"), Hmac(canonical), SyntheticKey));
    }
}
