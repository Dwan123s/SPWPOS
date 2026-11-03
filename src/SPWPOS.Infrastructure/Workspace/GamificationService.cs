using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static readonly (string Code, string Name, long Xp)[] Skins = [("classic", "NORA mặc định", 0)];

    public async Task<GamificationProfile> GetGamificationAsync(Guid uid, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == uid, ct);
        var level = (int)Math.Min(int.MaxValue, 1 + user.ExperiencePoints / 100);
        var fox = await db.OwnedPetSkins.AsNoTracking().AnyAsync(x => x.UserId == uid && x.SkinCode == "fox", ct);
        return new(user.ExperiencePoints, level, (long)level * 100, user.NoraSkin == "fox" && fox ? "fox" : "classic",
            [..Skins.Select(x => new SkinView(x.Code, x.Name, x.Xp, user.ExperiencePoints >= x.Xp)), new("fox", "MILO · Cáo nhỏ", 0, fox, 10000)],
            level >= 10 ? 3 : level >= 5 ? 2 : 1, level >= 10 ? null : level >= 5 ? 10 : 5);
    }
    public Task SelectSkinAsync(Guid uid, SkinInput input, CancellationToken ct) => Mutate(uid, async () => {
        if (input.Code == "fox")
        {
            Require(await db.OwnedPetSkins.AnyAsync(x => x.UserId == uid && x.SkinCode == "fox", ct), "SKIN_NOT_OWNED", "Mua skin MILO trước khi sử dụng.", 409);
            var owner = await db.Users.SingleAsync(x => x.Id == uid, ct);
            owner.NoraSkin = "fox"; owner.ConcurrencyStamp = Guid.NewGuid().ToString(); return;
        }
        var skin = Skins.SingleOrDefault(x => x.Code == input.Code);
        Require(skin.Code is not null, "SKIN_NOT_FOUND", "Trang phục này chưa có trong tủ đồ.", 404);
        var user = await db.Users.SingleAsync(x => x.Id == uid, ct);
        Require(user.ExperiencePoints >= skin.Xp, "SKIN_LOCKED", "Bạn chưa đủ XP để mở trang phục này.", 409);
        user.NoraSkin = skin.Code!; user.ConcurrencyStamp = Guid.NewGuid().ToString();
    }, ct, false);

    private async Task AwardTaskExperienceAsync(Guid uid, WorkTask task, DateTimeOffset completedAt, CancellationToken ct)
    {
        // Durable per-task flag survives reopening and retries. Completion dates
        // come from the server, including a recorded catch-up session's end.
        if (task.TaskExperienceAwarded || task.IsDeleted || task.Status != WorkTaskStatus.Completed ||
            completedAt > task.Deadline || await Mine<WorkTask>(uid).AnyAsync(x => x.ParentId == task.Id && !x.IsDeleted, ct)) return;
        var user = await db.Users.SingleAsync(x => x.Id == uid, ct);
        task.TaskExperienceAwarded = true;
        user.ExperiencePoints = checked(user.ExperiencePoints + 25);
        user.UserLevel = (int)Math.Min(int.MaxValue, 1 + user.ExperiencePoints / 100);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await AddNotification(uid, "TASK_XP", "+25 XP · Hoàn thành đúng hạn", $"Bạn nhận 25 XP khi hoàn thành “{task.Name}” đúng hạn.", "task-xp:" + task.Id, task.Id, ct);
    }

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
