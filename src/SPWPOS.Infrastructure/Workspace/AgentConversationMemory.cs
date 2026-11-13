using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private const int AgentHistoryTurns = 32;
    private const int AgentContextCharacters = 64000;
    private const string AgentAnswerSeparator = "\nBổ sung: ";

    private static string BoundedAgentRequest(string text, int limit)
    {
        if (text.Length <= limit) return text;
        // Pin the original request and retain the most recent answers.
        const string omitted = "\n[…]\n";
        return text[..4000] + omitted + text[^(limit - 4000 - omitted.Length)..];
    }

    private static AgentMessage[] ConversationHistory(AgentTurn[] newestFirst)
    {
        var messages = new List<AgentMessage>();
        var remaining = AgentContextCharacters;
        foreach (var row in newestFirst)
        {
            var view = TurnView(row);
            var reply = row.Status == "FAILED"
                ? "Yêu cầu thất bại; chưa lưu thay đổi từ yêu cầu này. Kiểm tra dữ liệu hiện tại trước khi trả lời."
                : view.Reply + " " + view.Outcome + " " + view.Question;
            if (row.Text.Length + reply.Length > remaining) break;
            messages.Add(new(row.Text, reply, row.Status));
            remaining -= row.Text.Length + reply.Length;
        }
        messages.Reverse();
        return messages.ToArray();
    }

    private async Task<string> ContinuedAgentRequestAsync(Guid uid, AgentRun run, Guid turnId, string latest, CancellationToken ct)
    {
        // Goal remains compatible with the existing 8,000-character column.
        // Rehydrate answers from account/conversation-scoped chat records instead
        // of repeatedly appending to that already-truncated copy.
        var turnCreatedAt = await Mine<AgentTurn>(uid).Where(x => x.Id == turnId).Select(x => x.CreatedAt).SingleAsync(ct);
        var first = await Mine<AgentTurn>(uid).AsNoTracking()
            .Where(x => x.ConversationId == run.ConversationId && x.CreatedAt <= run.CreatedAt && x.Status != "DELETED")
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (first?.Id == turnId) return latest;
        var original = first?.Text ?? run.Goal.Split(AgentAnswerSeparator, 2)[0];
        // Preserve the trusted calendar-type hint added to the initial input.
        var separator = run.Goal.IndexOf(AgentAnswerSeparator, StringComparison.Ordinal);
        var storedOriginal = separator < 0 ? run.Goal : run.Goal[..separator];
        if (storedOriginal.StartsWith(original, StringComparison.Ordinal)) original = storedOriginal;
        var answers = await Mine<AgentTurn>(uid).AsNoTracking()
            .Where(x => x.ConversationId == run.ConversationId && x.CreatedAt > run.CreatedAt && x.CreatedAt < turnCreatedAt &&
                x.Status != "DELETED" && x.Status != "PROCESSING")
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => x.Text).Take(63).ToArrayAsync(ct);
        return BoundedAgentRequest(string.Join(AgentAnswerSeparator, answers.Reverse().Prepend(original).Append(latest)), AgentContextCharacters);
    }
}
