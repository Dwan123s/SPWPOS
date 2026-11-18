using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact]
    public async Task Expired_overcapacity_team_warns_then_suspends_and_renewal_restores_it()
    {
        await using var h = await Setup();
        var expired = h.Clock.GetUtcNow();
        await h.DbAsync(async db =>
        {
            for (var i = 0; i < 4; i++)
            {
                var id = Guid.NewGuid();
                db.Users.Add(new() { Id = id, Email = id + "@example.com", NormalizedEmail = (id + "@example.com").ToUpperInvariant(), UserName = id.ToString(), NormalizedUserName = id.ToString().ToUpperInvariant(), IsActive = true, EmailConfirmed = true });
                db.TeamMemberships.Add(new() { TeamId = Team, UserId = id });
            }
            db.TeamPlanPurchases.Add(new() { UserId = Leader, PlanCode = "premium20", AmountVnd = 50000, OrderCode = 667,
                PaidAt = expired.AddMonths(-1), ValidUntil = expired });
            db.TelegramConnections.Add(new() { UserId = Leader, ChatId = 123, Enabled = true, ConnectedAt = expired.AddDays(-1) });
            await db.SaveChangesAsync(Ct);
        });
        var grace = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct));
        Assert.Equal("GracePeriod", grace.Renewal!.Status);
        Assert.Equal(expired.AddDays(3), grace.Renewal.GraceUntil);
        await Use(h, s => s.SendTeamChatAsync(Leader, Team, new(Guid.NewGuid(), "Trong thời gian gia hạn"), Ct));
        await Workspace(h, async s => { await s.EnsureNotificationsAsync(Leader, Ct); return true; });
        await Workspace(h, async s => { await s.EnsureNotificationsAsync(Leader, Ct); return true; });
        var notice = Assert.Single(await Workspace(h, s => s.GetNotificationsAsync(Leader, Ct)), x => x.Kind == "TEAM_PREMIUM_EXPIRED");
        Assert.Equal(Team, notice.TeamId);
        Assert.True(await h.DbAsync(db => db.InAppNotifications.AnyAsync(x => x.Id == notice.Id && x.TelegramPending, Ct)));
        h.Clock.Advance(TimeSpan.FromDays(3));
        var suspended = await Use(h, s => s.GetTeamAsync(Member, Team, Ct));
        Assert.Equal("Suspended", suspended.Renewal!.Status);
        Assert.Empty(suspended.Tasks);
        Assert.Equal(6, suspended.Members.Length);
        Assert.Equal("TEAM_SUSPENDED", (await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SendTeamChatAsync(Member, Team, new(Guid.NewGuid(), "Bị chặn"), Ct)))).Code);
        Assert.Equal("TEAM_SUSPENDED", (await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamChatAsync(Member, Team, null, null, Ct)))).Code);
        await Workspace(h, async s => { await s.EnsureNotificationsAsync(Leader, Ct); return true; });
        Assert.Single(await Workspace(h, s => s.GetNotificationsAsync(Leader, Ct)), x => x.Kind == "TEAM_PREMIUM_SUSPENDED");
        await h.DbAsync(async db =>
        {
            db.TeamPlanPurchases.Add(new() { UserId = Leader, PlanCode = "premium20", AmountVnd = 50000, OrderCode = 668,
                PaidAt = h.Clock.GetUtcNow(), ValidUntil = h.Clock.GetUtcNow().AddMonths(1) });
            await db.SaveChangesAsync(Ct);
        });
        var restored = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct));
        Assert.Null(restored.Renewal);
        Assert.Single(restored.Tasks);
        await Use(h, s => s.SendTeamChatAsync(Member, Team, new(Guid.NewGuid(), "Đã mở lại"), Ct));
    }

    [Fact]
    public async Task Expired_team_within_free_capacity_stays_available()
    {
        await using var h = await Setup();
        await h.DbAsync(async db =>
        {
            db.TeamPlanPurchases.Add(new() { UserId = Leader, PlanCode = "premium20", OrderCode = 669,
                PaidAt = h.Clock.GetUtcNow().AddMonths(-2), ValidUntil = h.Clock.GetUtcNow().AddDays(-4) });
            await db.SaveChangesAsync(Ct);
        });
        Assert.Null((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Renewal);
        await Use(h, s => s.SendTeamChatAsync(Member, Team, new(Guid.NewGuid(), "Vẫn sử dụng Free"), Ct));
    }
}
