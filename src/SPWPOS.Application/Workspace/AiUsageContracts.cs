namespace SPWPOS.Application.Workspace;

public sealed record AiUsageView(DateTimeOffset ResetsAt, int DailyLimit, int Used, int Active, int ConcurrentLimit,
    long? InputTokens, long? OutputTokens, int UnknownUsageCalls, long? EstimatedCostMicros = null, string? PricingVersion = null);
public interface IAiUsageService
{
    Task<T> RunAsync<T>(Guid userId, string operation, Func<Task<T>> action, CancellationToken ct);
    Task<AiUsageView> SummaryAsync(Guid userId, CancellationToken ct);
}
