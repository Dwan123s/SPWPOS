using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static string AgentFingerprint(AgentTurnInput input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{input.ConversationId:D}\n{input.Text.Trim()}\n{input.UseCapacityFactor}\n{input.UseEstimateBias}" +
        (input.Image is null ? "" : $"\n{input.Image.Name}\n{input.Image.MimeType}\n{input.Image.Data}"))));
    private static string PageCursor(DateTimeOffset date, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{date.UtcTicks}:{id:D}"));
    private static (DateTimeOffset Date, Guid Id)? ReadCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return null;
        Require(cursor.Length <= 160, "INVALID_CURSOR", "Con trỏ phân trang không hợp lệ.");
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out var id)) throw new FormatException();
            return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        { throw new SPWPOS.Application.Authentication.AuthException("INVALID_CURSOR", "Con trỏ phân trang không hợp lệ.", 400); }
    }
    private static ConversationView Conversation(AgentConversation row, int count) =>
        new(row.Id, row.Title, row.CreatedAt, row.UpdatedAt, row.ArchivedAt != null, count, row.Version);

    public async Task<ConversationPage> GetConversationsAsync(Guid uid, string? search, bool archived, string? cursor, int pageSize, CancellationToken ct)
    {
        Require(pageSize is >= 1 and <= 100 && (search?.Length ?? 0) <= 100, "VALIDATION_ERROR", "Phân trang hoặc từ khóa không hợp lệ.");
        var term = (search ?? "").Trim(); var before = ReadCursor(cursor);
        var query = Mine<AgentConversation>(uid).AsNoTracking().Where(x => x.DeletedAt == null && (x.ArchivedAt != null) == archived);
        if (term.Length > 0) query = query.Where(x => x.Title.Contains(term) || db.AgentTurns.Any(t => t.UserId == uid && t.ConversationId == x.Id && t.Status != "DELETED" && (t.Text.Contains(term) || t.ResultJson.Contains(term))));
        if (before is { } key) query = query.Where(x => x.UpdatedAt < key.Date || x.UpdatedAt == key.Date && x.Id.CompareTo(key.Id) < 0);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).Take(pageSize + 1)
            .Select(x => new { Row = x, Count = db.AgentTurns.Count(t => t.UserId == uid && t.ConversationId == x.Id && t.Status != "DELETED") }).ToArrayAsync(ct);
        var page = rows.Take(pageSize).ToArray();
        return new(page.Select(x => Conversation(x.Row, x.Count)).ToArray(), rows.Length > pageSize ? PageCursor(page[^1].Row.UpdatedAt, page[^1].Row.Id) : null);
    }
    private async Task<AgentConversation> FindConversation(Guid uid, Guid id, CancellationToken ct) =>
        await Mine<AgentConversation>(uid).SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
        ?? throw new SPWPOS.Application.Authentication.AuthException("NOT_FOUND", "Không tìm thấy hội thoại.", 404);
    public async Task<AgentTurnPage> GetConversationTurnsAsync(Guid uid, Guid conversationId, string? cursor, int pageSize, CancellationToken ct)
    {
        Require(pageSize is >= 1 and <= 100, "VALIDATION_ERROR", "Mỗi trang từ 1 đến 100 yêu cầu.");
        await FindConversation(uid, conversationId, ct); var before = ReadCursor(cursor);
        var query = Mine<AgentTurn>(uid).AsNoTracking().Where(x => x.ConversationId == conversationId && x.Status != "DELETED");
        if (before is { } key) query = query.Where(x => x.CreatedAt < key.Date || x.CreatedAt == key.Date && x.Id.CompareTo(key.Id) < 0);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(pageSize + 1).ToArrayAsync(ct);
        var revision = await Mine<WorkPreference>(uid).Select(x => x.Revision).SingleOrDefaultAsync(ct);
        var page = rows.Take(pageSize).ToArray();
        return new(page.Reverse().Select(x => TurnView(x) with { CanUndo = x.Status == "APPLIED" && x.AppliedRevision == revision }).ToArray(),
            rows.Length > pageSize ? PageCursor(page[^1].CreatedAt, page[^1].Id) : null);
    }
    public Task<ConversationView> EditConversationAsync(Guid uid, Guid conversationId, ConversationEdit input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(!string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 120, "VALIDATION_ERROR", "Tên hội thoại từ 1 đến 120 ký tự.");
        var row = await FindConversation(uid, conversationId, ct);
        Require(row.Version == input.Version, "CONVERSATION_STALE", "Hội thoại vừa thay đổi. Hãy tải lại trước khi chỉnh sửa.", 409);
        Require(!await Mine<AgentTurn>(uid).AnyAsync(x => x.ConversationId == conversationId && x.Status == "PROCESSING", ct), "AGENT_BUSY", "Chờ NORA hoàn tất trước khi sửa hội thoại.", 409);
        row.Title = input.Title.Trim(); row.ArchivedAt = input.Archived ? Now : null; row.UpdatedAt = Now; row.Version = Guid.NewGuid();
        return Conversation(row, await Mine<AgentTurn>(uid).CountAsync(x => x.ConversationId == conversationId && x.Status != "DELETED", ct));
    }, ct, false);
    public Task DeleteConversationAsync(Guid uid, Guid conversationId, Guid version, CancellationToken ct) => Mutate(uid, async () =>
    {
        var row = await FindConversation(uid, conversationId, ct);
        Require(row.Version == version, "CONVERSATION_STALE", "Hội thoại vừa thay đổi. Hãy tải lại trước khi xóa.", 409);
        Require(!await Mine<AgentTurn>(uid).AnyAsync(x => x.ConversationId == conversationId && x.Status == "PROCESSING", ct), "AGENT_BUSY", "Chờ NORA hoàn tất trước khi xóa hội thoại.", 409);
        // Preserve only the request identity and fingerprint. Deleted chat content
        // cannot be read/exported, while a network replay cannot repeat mutations.
        foreach (var turn in await Mine<AgentTurn>(uid).Where(x => x.ConversationId == conversationId).ToArrayAsync(ct))
        {
            if (turn.InputFingerprint.Length == 0) turn.InputFingerprint = AgentFingerprint(new(turn.ConversationId, turn.RequestId, turn.Text));
            turn.Text = ""; turn.CommandJson = ""; turn.ChangesJson = ""; turn.ResultJson = ""; turn.UndoJson = "";
            turn.Status = "DELETED"; turn.UpdatedAt = Now;
        }
        row.Title = ""; row.DeletedAt = Now; row.ArchivedAt = null; row.Version = Guid.NewGuid(); row.UpdatedAt = Now;
    }, ct, false);
    public async Task<ConversationExport> ExportConversationAsync(Guid uid, Guid conversationId, CancellationToken ct)
    {
        var row = await FindConversation(uid, conversationId, ct);
        var turns = await Mine<AgentTurn>(uid).AsNoTracking().Where(x => x.ConversationId == conversationId && x.Status != "DELETED")
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(10001).ToArrayAsync(ct);
        return new(Conversation(row, await Mine<AgentTurn>(uid).CountAsync(x => x.ConversationId == conversationId && x.Status != "DELETED", ct)),
            turns.Take(10000).Select(TurnView).ToArray(), turns.Length > 10000);
    }
}
