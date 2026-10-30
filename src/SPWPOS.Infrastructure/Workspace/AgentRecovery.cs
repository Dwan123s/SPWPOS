using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private async Task<AgentIntent> InterpretWithRecoveryAsync(Guid uid,
        Func<CancellationToken, Task<AgentIntent>> interpret, Func<bool> replyStarted,
        Func<Task> notifyRetry, CancellationToken ct)
    {
        try { return await RunJob(uid, "AI_REQUEST", () => interpret(ct), ct); }
        catch (AuthException error) when (!ct.IsCancellationRequested && !replyStarted() &&
            error.Code is "AGENT_AI_TIMEOUT" or "AGENT_AI_UNAVAILABLE")
        {
            // Exactly one new interpretation; never replay a calendar mutation.
            // Each attempt has its own quota reservation and usage/job record.
            await notifyRetry();
            await Task.Delay(Random.Shared.Next(750, 1501), ct);
            return await RunJob(uid, "AI_REQUEST", () => interpret(ct), ct);
        }
    }
}
