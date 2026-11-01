using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    public async Task<TeamChatPage> GetTeamChatAsync(Guid uid, Guid teamId, long? after, long? before, CancellationToken ct)
    {
        await TeamMember(uid, teamId, ct);
        Require((after is null || after >= 0) && (before is null || before > 0) && !(after != null && before != null), "INVALID_CURSOR", "Mốc tin nhắn không hợp lệ.");
        var query = from m in db.TeamChatMessages.AsNoTracking() join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.TeamId == teamId && (after == null || m.Id > after) && (before == null || m.Id < before)
            select new { m.Id, m.UserId, Author = u.FirstName + " " + u.LastName, m.Text, m.CreatedAt, m.IsSystem };
        var descending = after == null;
        var messages = await (descending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id)).Take(51).ToArrayAsync(ct);
        var page = messages.Take(50); if (descending) page = page.Reverse(); var items = page.ToArray();
        // Send each account avatar once per page, rather than repeat image data on every message.
        var authorIds = items.Where(x => !x.IsSystem).Select(x => x.UserId).Distinct().ToArray();
        var authors = await (from u in db.Users.AsNoTracking()
            join p in db.UserProfiles.AsNoTracking() on u.Id equals p.UserId into profiles
            from p in profiles.DefaultIfEmpty()
            where authorIds.Contains(u.Id)
            select new TeamChatAuthorView(u.Id, p != null && p.DisplayName != "" ? p.DisplayName : u.FirstName + " " + u.LastName,
                p != null ? p.AvatarUrl : "")).ToArrayAsync(ct);
        return new(items.Select(x => new TeamChatMessageView(x.Id, x.UserId, x.IsSystem ? "Hệ thống" : x.Author, x.Text, x.CreatedAt, x.IsSystem)).ToArray(), items.Length > 0 ? items[^1].Id : after, messages.Length > 50, authors);
    }
    public Task<TeamChatMessageView> SendTeamChatAsync(Guid uid, Guid teamId, TeamChatInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        var member = await TeamMember(uid, teamId, ct);
        Require(input.RequestId != Guid.Empty && !string.IsNullOrWhiteSpace(input.Text) && input.Text.Length <= 2000, "VALIDATION_ERROR", "Tin nhắn cần 1–2000 ký tự.");
        var message = await db.TeamChatMessages.SingleOrDefaultAsync(x => x.TeamId == teamId && x.UserId == uid && x.RequestId == input.RequestId && !x.IsSystem, ct);
        if (message is null)
        {
            member.MutationVersion = Guid.NewGuid();
            message = new TeamChatMessage { TeamId = teamId, UserId = uid, RequestId = input.RequestId, Text = input.Text.Trim(), CreatedAt = Now };
            db.TeamChatMessages.Add(message); await db.SaveChangesAsync(ct);
        }
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == uid, ct);
        return new TeamChatMessageView(message.Id, uid, message.IsSystem ? "Hệ thống" : user.FirstName + " " + user.LastName, message.Text, message.CreatedAt, message.IsSystem);
    }, ct, false);
}
