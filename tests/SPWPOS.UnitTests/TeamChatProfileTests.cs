using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Chat_shares_only_public_author_identity_and_sends_avatar_once_per_account()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => {
            db.UserProfiles.AddRange(new UserProfile { UserId = Member, DisplayName = "An Nguyễn", AvatarUrl = "/assets/member.png", Bio = "PRIVATE_BIO" },
                new UserProfile { UserId = Other, DisplayName = "OUTSIDE_ACCOUNT", AvatarUrl = "/assets/outside.png" });
            await db.SaveChangesAsync(Ct);
        });
        await Use(h, s => s.SendTeamChatAsync(Member, Team, new(Guid.NewGuid(), "Tin một"), Ct));
        await Use(h, s => s.SendTeamChatAsync(Member, Team, new(Guid.NewGuid(), "Tin hai"), Ct));
        await Use(h, s => s.SendTeamChatAsync(Leader, Team, new(Guid.NewGuid(), "Không có ảnh đại diện"), Ct));
        var page = await Use(h, s => s.GetTeamChatAsync(Leader, Team, null, null, Ct));
        Assert.Equal(3, page.Items.Length); Assert.Equal(2, page.Authors!.Length);
        var author = Assert.Single(page.Authors, x => x.UserId == Member);
        Assert.Equal("An Nguyễn", author.Name); Assert.Equal("/assets/member.png", author.AvatarUrl);
        Assert.Equal("", Assert.Single(page.Authors, x => x.UserId == Leader).AvatarUrl);
        var json = JsonSerializer.Serialize(page);
        Assert.DoesNotContain("PRIVATE_BIO", json); Assert.DoesNotContain("OUTSIDE_ACCOUNT", json); Assert.DoesNotContain("@example.com", json);
        Assert.DoesNotContain("ExperiencePoints", json); Assert.DoesNotContain("OwnedPetSkins", json);
        var detail = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct));
        var member = Assert.Single(detail.Members, x => x.UserId == Member);
        Assert.Equal("An Nguyễn", member.Name); Assert.Equal("/assets/member.png", member.AvatarUrl);
        Assert.Equal("", Assert.Single(detail.Members, x => x.UserId == Leader).AvatarUrl);
        var detailJson = JsonSerializer.Serialize(detail);
        Assert.DoesNotContain("PRIVATE_BIO", detailJson); Assert.DoesNotContain("OUTSIDE_ACCOUNT", detailJson); Assert.DoesNotContain("@example.com", detailJson);
        await h.DbAsync(async db => { (await db.UserProfiles.SingleAsync(x => x.UserId == Member, Ct)).AvatarUrl = "/assets/member-new.png"; await db.SaveChangesAsync(Ct); });
        Assert.Equal("/assets/member-new.png", Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Members, x => x.UserId == Member).AvatarUrl);
        var empty = await Use(h, s => s.GetTeamChatAsync(Leader, Team, page.NextAfter, null, Ct)); Assert.Empty(empty.Items);
        Assert.Equal("/assets/member-new.png", Assert.Single(empty.Authors!, x => x.UserId == Member).AvatarUrl);
    }
    [Fact] public async Task Chat_shows_only_equipped_owned_skin_and_refreshes_pet_without_new_messages()
    {
        await using var h = await Setup();
        await Use(h, s => s.SendTeamChatAsync(Member, Team, new(Guid.NewGuid(), "Khoe pet"), Ct));
        await h.DbAsync(async db => {
            var user = await db.Users.SingleAsync(x => x.Id == Member, Ct);
            user.NoraSkin = "fox"; user.ExperiencePoints = 900; user.UserLevel = 1;
            await db.SaveChangesAsync(Ct);
        });
        var first = await Use(h, s => s.GetTeamChatAsync(Leader, Team, null, null, Ct));
        var fallback = Assert.Single(first.Authors!);
        Assert.Equal("classic", fallback.SelectedSkin); Assert.Equal(10, fallback.UserLevel);
        await h.DbAsync(async db => {
            var purchase = new SkinPurchase { UserId = Member, RequestId = Guid.NewGuid(), OrderCode = 111, Status = "PAID", ExpiresAt = h.Clock.GetUtcNow().AddMinutes(15) };
            db.SkinPurchases.Add(purchase);
            db.OwnedPetSkins.Add(new() { UserId = Member, SkinCode = "fox", PurchaseId = purchase.Id, AcquiredAt = h.Clock.GetUtcNow() });
            await db.SaveChangesAsync(Ct);
        });
        var refreshed = await Use(h, s => s.GetTeamChatAsync(Leader, Team, first.NextAfter, null, Ct));
        Assert.Empty(refreshed.Items);
        var fox = Assert.Single(refreshed.Authors!);
        Assert.Equal("fox", fox.SelectedSkin); Assert.Equal(10, fox.UserLevel);
        await h.DbAsync(async db => { (await db.Users.SingleAsync(x => x.Id == Member, Ct)).NoraSkin = "classic"; await db.SaveChangesAsync(Ct); });
        var equipped = Assert.Single((await Use(h, s => s.GetTeamChatAsync(Leader, Team, first.NextAfter, null, Ct))).Authors!);
        Assert.Equal("classic", equipped.SelectedSkin);
    }
}
