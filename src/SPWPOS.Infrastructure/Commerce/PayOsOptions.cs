namespace SPWPOS.Infrastructure.Commerce;

public sealed class PayOsOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string CheckoutReturnUrl => new Uri(new Uri(PublicBaseUrl), "/?payment=returned").AbsoluteUri;
    public bool Ready => Enabled && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ChecksumKey) && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var url) &&
        url.Scheme == "https" && !url.IsLoopback && url.UserInfo.Length == 0 && url.Query.Length == 0 && url.Fragment.Length == 0 && url.AbsolutePath == "/";
}
public sealed record PayOsLink(long OrderCode, int AmountVnd, string Currency, string Id, string CheckoutUrl, string AccountNumber);
public sealed record PayOsTransaction(int Amount, string Reference, string AccountNumber);
// Returned only after verifying the signed merchant API response.
public sealed record PayOsPaymentState(long OrderCode, int AmountVnd, string Id, string Status,
    int AmountPaid, int AmountRemaining, PayOsTransaction[] Transactions);
public interface IPayOsGateway
{
    Task<PayOsLink?> FindAsync(long orderCode, CancellationToken ct);
    Task<PayOsPaymentState?> GetPaymentAsync(long orderCode, CancellationToken ct);
    Task<PayOsLink> CreateAsync(long orderCode, int amount, DateTimeOffset expiresAt, CancellationToken ct);
}
