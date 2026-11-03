using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace SPWPOS.Infrastructure.Commerce;

public sealed record PayOsVerifiedPayment(long OrderCode, long AmountVnd, string Currency,
    string PaymentLinkId, string Reference, string AccountNumber, bool Succeeded);

// payment-requests protocol only. This is not the payouts signature protocol.
// The order processor must still match a verified receipt to the stored order,
// channel, amount and paymentLinkId before granting any entitlement.
public static class PayOsPaymentSignatures
{
    public static string SignCheckout(long orderCode, long amountVnd, string description,
        Uri cancelUrl, Uri returnUrl, string checksumKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checksumKey);
        if (orderCode is <= 0 or > 9007199254740991 || amountVnd <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderCode));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Length > 9 || description.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Use a short server generated ASCII order description.", nameof(description));
        ValidateReturnUrl(cancelUrl); ValidateReturnUrl(returnUrl);
        var canonical = $"amount={amountVnd.ToString(CultureInfo.InvariantCulture)}&cancelUrl={cancelUrl.AbsoluteUri}" +
            $"&description={description}&orderCode={orderCode.ToString(CultureInfo.InvariantCulture)}&returnUrl={returnUrl.AbsoluteUri}";
        return Convert.ToHexString(Mac(canonical, checksumKey)).ToLowerInvariant();
    }

    private static void ValidateReturnUrl(Uri value)
    {
        if (!value.IsAbsoluteUri || value.Scheme != Uri.UriSchemeHttps || value.UserInfo.Length > 0 || value.Fragment.Length > 0)
            throw new ArgumentException("Return URLs must be server configured absolute HTTPS URLs without fragments or credentials.");
    }
    private static byte[] Mac(string value, string key) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value));

    public static bool VerifyData(JsonElement data, string? signature, string checksumKey, bool allowTransactions = false)
    {
        if (string.IsNullOrWhiteSpace(checksumKey) || signature?.Length != 64 || data.ValueKind != JsonValueKind.Object) return false;
        try
        {
            // The payment-information API signs transactions as compact JSON,
            // preserving array order and sorting each transaction's keys. This
            // matches payOS' official CryptoProvider, not the payouts protocol.
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in data.EnumerateObject())
            {
                if (fields.ContainsKey(property.Name) || property.Name.Length == 0) return false;
                string value;
                switch (property.Value.ValueKind)
                {
                    case JsonValueKind.Null: value = ""; break;
                    case JsonValueKind.String:
                        var text = property.Value.GetString()!;
                        value = text is "null" or "undefined" ? "" : text;
                        break;
                    case JsonValueKind.Number:
                        if (!property.Value.TryGetInt64(out var number)) return false;
                        value = number.ToString(CultureInfo.InvariantCulture); break;
                    case JsonValueKind.True: value = "true"; break;
                    case JsonValueKind.False: value = "false"; break;
                    case JsonValueKind.Array when allowTransactions && property.Name == "transactions":
                        if (!TryCanonicalTransactions(property.Value, out value)) return false;
                        break;
                    default: return false;
                }
                fields.Add(property.Name, value);
            }
            if (fields.Count == 0 || fields.Count > 100) return false;
            var canonical = string.Join('&', fields.Select(field => $"{field.Key}={field.Value}"));
            return CryptographicOperations.FixedTimeEquals(Mac(canonical, checksumKey), Convert.FromHexString(signature));
        }
        catch (FormatException) { return false; }
    }

    private static bool TryCanonicalTransactions(JsonElement transactions, out string value)
    {
        value = "";
        if (transactions.GetArrayLength() > 100) return false;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartArray();
            foreach (var transaction in transactions.EnumerateArray())
            {
                if (transaction.ValueKind != JsonValueKind.Object) return false;
                var properties = transaction.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
                if (properties.Length > 100 || properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) return false;
                writer.WriteStartObject();
                foreach (var property in properties)
                {
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined) return false;
                    if (property.Value.ValueKind == JsonValueKind.Number && !property.Value.TryGetInt64(out _)) return false;
                    writer.WritePropertyName(property.Name);
                    property.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        value = Encoding.UTF8.GetString(stream.ToArray());
        return true;
    }

    public static PayOsPaymentState? VerifyPaymentInformation(JsonElement root, string checksumKey)
    {
        try
        {
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count() ||
                root.GetProperty("code").GetString() != "00" ||
                !VerifyData(root.GetProperty("data"), root.GetProperty("signature").GetString(), checksumKey, allowTransactions: true)) return null;
            var data = root.GetProperty("data");
            var code = data.GetProperty("orderCode").GetInt64();
            var id = data.GetProperty("id").GetString();
            var status = data.GetProperty("status").GetString();
            var amount = data.GetProperty("amount").GetInt32();
            var paid = data.GetProperty("amountPaid").GetInt32();
            var remaining = data.GetProperty("amountRemaining").GetInt32();
            if (code is <= 0 or > 9007199254740991 || string.IsNullOrWhiteSpace(id) || id.Length > 100 ||
                status is not ("PAID" or "PENDING" or "PROCESSING" or "EXPIRED" or "CANCELLED" or "UNDERPAID") || amount <= 0 || paid < 0 || remaining < 0) return null;
            var transactions = new List<PayOsTransaction>();
            foreach (var transaction in data.GetProperty("transactions").EnumerateArray())
            {
                var reference = transaction.GetProperty("reference").GetString();
                var account = transaction.GetProperty("accountNumber").GetString();
                var transactionAmount = transaction.GetProperty("amount").GetInt32();
                if (transactionAmount <= 0 || string.IsNullOrWhiteSpace(reference) || reference.Length > 100 ||
                    string.IsNullOrWhiteSpace(account) || account.Length > 100 || transactions.Any(x => x.Reference == reference)) return null;
                transactions.Add(new(transactionAmount, reference, account));
            }
            return new(code, amount, id, status, paid, remaining, transactions.ToArray());
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { return null; }
    }

    public static PayOsVerifiedPayment? VerifyWebhook(ReadOnlyMemory<byte> body, string checksumKey)
    {
        if (body.Length is 0 or > 65536) return null;
        try
        {
            using var parsed = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count() ||
                !root.TryGetProperty("data", out var data) || !root.TryGetProperty("signature", out var signature) || signature.ValueKind != JsonValueKind.String ||
                !VerifyData(data, signature.GetString(), checksumKey)) return null;
            var order = data.GetProperty("orderCode").GetInt64(); var amount = data.GetProperty("amount").GetInt64();
            var currency = data.GetProperty("currency").GetString(); var link = data.GetProperty("paymentLinkId").GetString();
            var reference = data.GetProperty("reference").GetString(); var account = data.GetProperty("accountNumber").GetString();
            if (order is <= 0 or > 9007199254740991 || amount <= 0 || currency != "VND" || string.IsNullOrWhiteSpace(link) || link.Length > 100 ||
                string.IsNullOrWhiteSpace(reference) || reference.Length > 100 || string.IsNullOrWhiteSpace(account) || account.Length > 100) return null;
            // Only the signed data code is authoritative. The outer success flag
            // and return URL cannot authorize a payment or inventory grant.
            return new(order, amount, currency, link, reference, account, data.GetProperty("code").GetString() == "00");
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return null; }
    }
}
