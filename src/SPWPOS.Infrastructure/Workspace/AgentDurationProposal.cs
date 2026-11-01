using System.Globalization;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static int? ProposedDuration(string reply)
    {
        if (!ChatMatch(reply, @"\b(?:de xuat|uoc luong|uoc tinh)\b").Success) return null;
        var values = RequestMatches(MatchText(reply), @"\b(\d{1,5})\s*phut\b")
            .Select(x => int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().ToArray();
        return values is [var minutes] && minutes is >= 1 and <= 43200 ? minutes : null;
    }

    private static AgentPendingRequest? ConfirmDurationProposal(AgentPendingRequest? pending, string message)
    {
        if (pending?.ProposedMinutes is null) return pending;
        var text = MatchText(message).Trim().TrimEnd('.', '!');
        if (ChatMatch(text, @"\b(?:(?:khong|chua)\s+(?:dong y|chap nhan|xac nhan)|doi thoi luong)\b").Success)
            return pending with { DurationProposalAccepted = false };
        if (pending.DurationProposalAccepted) return pending;
        var accepts = ChatMatch(text, @"^(?:dong y|xac nhan|ok|okay|duoc|cu nhu vay|chap nhan)(?:\s+(?:voi\s+)?(?:uoc luong|de xuat|thoi luong)(?:\s+(?:nay|do|tren))?)?$").Success ||
            QuotedMinutes(text) == pending.ProposedMinutes &&
            ChatMatch(text, @"^(?:dong y|xac nhan|chap nhan)\b").Success && !ChatMatch(text, @"\b(?:khong|dung|chua)\b").Success;
        return accepts ? pending with { DurationProposalAccepted = true } : pending;
    }
}
