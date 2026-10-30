using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static readonly (string Code, string Name, long Xp)[] Skins = [
        ("classic", "NORA cổ điển", 0), ("forest", "NORA rừng xanh", 100), ("sunset", "NORA hoàng hôn", 300), ("cosmic", "NORA vũ trụ", 600) ];

    public async Task<GamificationProfile> GetGamificationAsync(Guid uid, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == uid, ct);
        return new(user.ExperiencePoints, user.UserLevel, (long)user.UserLevel * 100, user.NoraSkin,
            Skins.Select(x => new SkinView(x.Code, x.Name, x.Xp, user.ExperiencePoints >= x.Xp)).ToArray());
    }
    public Task SelectSkinAsync(Guid uid, SkinInput input, CancellationToken ct) => Mutate(uid, async () => {
        var skin = Skins.SingleOrDefault(x => x.Code == input.Code);
        Require(skin.Code is not null, "SKIN_NOT_FOUND", "Trang phục này chưa có trong tủ đồ.", 404);
        var user = await db.Users.SingleAsync(x => x.Id == uid, ct);
        Require(user.ExperiencePoints >= skin.Xp, "SKIN_LOCKED", "Bạn chưa đủ XP để mở trang phục này.", 409);
        user.NoraSkin = skin.Code!; user.ConcurrencyStamp = Guid.NewGuid().ToString();
    }, ct, false);

    private async Task AwardSessionExperienceAsync(Guid uid, WorkSession session, WorkTask task, DateTimeOffset completedAt, CancellationToken ct)
    {
        if (session.ExperienceAwarded || session.Status != WorkSessionStatus.Completed || session.ActualSeconds <= 0 ||
            completedAt > session.End || completedAt > task.Deadline) return;
        var user = await db.Users.SingleAsync(x => x.Id == uid, ct);
        session.ExperienceAwarded = true;
        user.ExperiencePoints = checked(user.ExperiencePoints + Math.Clamp((int)((session.End - session.Start).TotalMinutes / 15) + 10, 10, 50));
        user.UserLevel = (int)Math.Min(int.MaxValue, 1 + user.ExperiencePoints / 100);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
    }
}
