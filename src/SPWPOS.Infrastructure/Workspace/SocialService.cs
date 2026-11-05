using System.Data;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Workspace;

public sealed class SocialService(ApplicationDbContext db, TimeProvider clock) : ISocialService
{
    private sealed class PersonRow
    {
        public Guid Id { get; init; }
        public string DisplayName { get; init; } = "";
        public string Bio { get; init; } = "";
        public string AvatarUrl { get; init; } = "";
        public DateTimeOffset JoinedAt { get; init; }
    }
    private IQueryable<PersonRow> People => from u in db.Users.AsNoTracking()
        join p in db.UserProfiles.AsNoTracking() on u.Id equals p.UserId into profiles
        from p in profiles.DefaultIfEmpty()
        where u.IsActive && u.EmailConfirmed
        select new PersonRow { Id = u.Id, DisplayName = p != null && p.DisplayName != "" ? p.DisplayName : (u.FirstName + " " + u.LastName).Trim(),
            Bio = p != null ? p.Bio : "", AvatarUrl = p != null ? p.AvatarUrl : "", JoinedAt = u.CreatedAt };
    private static (Guid Low, Guid High) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);
    private IQueryable<Friendship> Link(Guid a, Guid b)
    { var (low, high) = Pair(a, b); return db.Friendships.Where(x => x.UserLowId == low && x.UserHighId == high); }
    private static string Relation(Friendship? f, Guid viewer, Guid target) => viewer == target ? "Self" :
        f is null ? "None" : f.Status == "Accepted" ? "Friends" : f.RequestedById == viewer ? "Outgoing" : "Incoming";
    private IQueryable<Guid> FriendIds(Guid owner) =>
        from f in db.Friendships.AsNoTracking()
        where f.Status == "Accepted" && (f.UserLowId == owner || f.UserHighId == owner)
        join u in db.Users on (f.UserLowId == owner ? f.UserHighId : f.UserLowId) equals u.Id
        where u.IsActive && u.EmailConfirmed
        select u.Id;
    private async Task Available(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id && u.IsActive && u.EmailConfirmed, ct))
            throw new AuthException("PROFILE_NOT_FOUND", "Không tìm thấy trang cá nhân này.", 404);
    }
    private static void PageBounds(int offset)
    { if (offset < 0 || offset > 10000) throw new AuthException("INVALID_PAGE", "Trang không hợp lệ."); }
    public async Task<SocialProfileView> ProfileAsync(Guid viewerId, Guid profileId, CancellationToken ct)
    {
        var person = await People.SingleOrDefaultAsync(x => x.Id == profileId, ct)
            ?? throw new AuthException("PROFILE_NOT_FOUND", "Không tìm thấy trang cá nhân này.", 404);
        var cover = await db.UserProfiles.AsNoTracking().Where(x => x.UserId == profileId).Select(x => x.CoverUrl).SingleOrDefaultAsync(ct) ?? "";
        return new(person.Id, person.DisplayName, person.Bio, person.AvatarUrl, cover, person.JoinedAt,
            Relation(await Link(viewerId, profileId).AsNoTracking().SingleOrDefaultAsync(ct), viewerId, profileId),
            await FriendIds(profileId).CountAsync(ct), viewerId == profileId ? 0 : await FriendIds(viewerId).Intersect(FriendIds(profileId)).CountAsync(ct));
    }
    private async Task<PeoplePage> Page(Guid viewerId, IQueryable<PersonRow> query, int offset, CancellationToken ct)
    {
        PageBounds(offset);
        var rows = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(offset).Take(25).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var links = await db.Friendships.AsNoTracking().Where(x => x.UserLowId == viewerId && ids.Contains(x.UserHighId)
            || x.UserHighId == viewerId && ids.Contains(x.UserLowId)).ToArrayAsync(ct);
        return new(rows.Take(24).Select(p => new PersonCard(p.Id, p.DisplayName, p.Bio, p.AvatarUrl,
            Relation(links.FirstOrDefault(f => f.UserLowId == p.Id || f.UserHighId == p.Id), viewerId, p.Id))).ToArray(), rows.Length > 24, rows.Length > 24 ? offset + 24 : null);
    }
    public Task<PeoplePage> PeopleAsync(Guid viewerId, string? search, int offset, CancellationToken ct)
    {
        var term = (search ?? "").Trim();
        if (term.Length > 100) throw new AuthException("INVALID_SEARCH", "Tên tìm kiếm tối đa 100 ký tự.");
        var query = People.Where(x => x.Id != viewerId);
        if (term.Length > 0) query = query.Where(x => x.DisplayName.ToUpper().Contains(term.ToUpper()));
        return Page(viewerId, query, offset, ct);
    }
    public async Task<PeoplePage> ConnectionsAsync(Guid viewerId, Guid ownerId, string kind, int offset, CancellationToken ct)
    {
        await Available(ownerId, ct);
        IQueryable<Guid> ids;
        if (kind == "friends") ids = FriendIds(ownerId);
        else {
            if (viewerId != ownerId) throw new AuthException("FORBIDDEN", "Lời mời kết bạn là thông tin riêng tư.", 403);
            var pending = db.Friendships.AsNoTracking().Where(x => x.Status == "Pending" && (x.UserLowId == ownerId || x.UserHighId == ownerId));
            if (kind == "incoming") pending = pending.Where(x => x.RequestedById != ownerId);
            else if (kind == "outgoing") pending = pending.Where(x => x.RequestedById == ownerId);
            else throw new AuthException("INVALID_FILTER", "Mục kết bạn không hợp lệ.");
            ids = pending.Select(x => x.UserLowId == ownerId ? x.UserHighId : x.UserLowId);
        }
        return await Page(viewerId, People.Where(x => ids.Contains(x.Id)), offset, ct);
    }
    private async Task<FriendshipResult> Change(Guid uid, Guid other, string action, CancellationToken ct)
    {
        if (uid == other) throw new AuthException("SELF_FRIENDSHIP", "Bạn không thể kết bạn với chính mình.");
        await Available(uid, ct); await Available(other, ct);
        var (low, high) = Pair(uid, other);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Key-range locks serialize all decisions for the same pair, including two simultaneous first requests.
        var link = db.Database.IsSqlServer()
            ? await db.Friendships.FromSqlInterpolated($"SELECT * FROM [Friendships] WITH (UPDLOCK,HOLDLOCK) WHERE [UserLowId] = {low} AND [UserHighId] = {high}").SingleOrDefaultAsync(ct)
            : await Link(uid, other).SingleOrDefaultAsync(ct);
        if (action == "request") {
            if (link is null) {
                link = new() { UserLowId = low, UserHighId = high, RequestedById = uid, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
                db.Friendships.Add(link);
                await Notify(uid, other, "FRIEND_REQUEST", "Bạn có lời mời kết bạn mới", $"social:request:{low:N}:{high:N}:{link.Version:N}", ct);
            }
        } else if (action == "accept") {
            if (link?.Status == "Accepted") return new("Friends");
            if (link is null || link.RequestedById == uid) throw new AuthException("FRIEND_REQUEST_STALE", "Lời mời không còn khả dụng. Hãy tải lại danh sách.", 409);
            link.Status = "Accepted"; link.UpdatedAt = clock.GetUtcNow(); link.Version = Guid.NewGuid();
            await Notify(uid, other, "FRIEND_ACCEPTED", "Lời mời kết bạn đã được chấp nhận", $"social:accepted:{link.Version:N}", ct);
        } else if (link is not null) db.Friendships.Remove(link);
        if (action != "request") {
            var prefix = $"social:request:{low:N}:{high:N}:";
            var notices = await db.InAppNotifications.Where(x => x.DedupeKey.StartsWith(prefix)).ToArrayAsync(ct);
            foreach (var notice in notices) { notice.ReadAt ??= clock.GetUtcNow(); notice.IsVisible = false; }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(action == "remove" ? "None" : Relation(link, uid, other));
    }
    private async Task Notify(Guid actor, Guid recipient, string kind, string title, string key, CancellationToken ct)
    {
        var name = await People.Where(x => x.Id == actor).Select(x => x.DisplayName).SingleAsync(ct);
        var visible = await db.NotificationSettings.Where(x => x.UserId == recipient).Select(x => (bool?)x.InAppEnabled).SingleOrDefaultAsync(ct) ?? true;
        db.InAppNotifications.Add(new() { UserId = recipient, Kind = kind, Title = title,
            Message = kind == "FRIEND_REQUEST" ? $"{name} muốn kết bạn với bạn." : $"{name} đã chấp nhận lời mời. Hai bạn giờ đã là bạn bè.",
            DedupeKey = key, CreatedAt = clock.GetUtcNow(), ScheduledFor = clock.GetUtcNow(), IsVisible = visible });
    }
    public Task<FriendshipResult> RequestAsync(Guid userId, Guid otherId, CancellationToken ct) => Change(userId, otherId, "request", ct);
    public Task<FriendshipResult> AcceptAsync(Guid userId, Guid otherId, CancellationToken ct) => Change(userId, otherId, "accept", ct);
    public Task<FriendshipResult> RemoveAsync(Guid userId, Guid otherId, CancellationToken ct) => Change(userId, otherId, "remove", ct);
    public async Task UpdateCoverAsync(Guid userId, ProfileCoverInput input, CancellationToken ct)
    {
        await Available(userId, ct);
        var url = (input.CoverUrl ?? "").Trim();
        if (url != "") {
            var comma = url.IndexOf(',');
            if (comma < 0 || url.Length > 3000000 || url[..comma] is not ("data:image/jpeg;base64" or "data:image/png;base64" or "data:image/webp;base64"))
                throw new AuthException("INVALID_COVER", "Ảnh bìa phải là ảnh PNG, JPG hoặc WebP, tối đa 2 MB.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(url[(comma + 1)..]); }
            catch (FormatException) { throw new AuthException("INVALID_COVER", "Ảnh bìa không hợp lệ."); }
            var valid = url.StartsWith("data:image/jpeg;", StringComparison.Ordinal) ? bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 :
                url.StartsWith("data:image/png;", StringComparison.Ordinal) ? bytes.AsSpan().StartsWith(new byte[] { 137,80,78,71,13,10,26,10 }) :
                bytes.Length >= 12 && bytes.AsSpan(0,4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8,4).SequenceEqual("WEBP"u8);
            if (!valid || bytes.Length > 2 * 1024 * 1024) throw new AuthException("INVALID_COVER", "Ảnh bìa không hợp lệ hoặc lớn hơn 2 MB.");
        }
        var profile = await db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null) { profile = new() { UserId = userId }; db.UserProfiles.Add(profile); }
        profile.CoverUrl = url; profile.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }
}
