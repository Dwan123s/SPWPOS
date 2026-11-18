using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Theory]
    [InlineData("free", 5, true)]
    [InlineData("premium20", 19, false)]
    [InlineData("premium20", 20, true)]
    [InlineData("premiumUnlimited", 51, false)]
    public async Task Approval_enforces_paid_plan_capacity_including_the_leader(string code, int activeCount, bool blocked)
    {
        await using var h = await Setup();
        await h.DbAsync(async db =>
        {
            for (var i = 2; i < activeCount; i++)
            {
                var user = new ApplicationUser { Id = Guid.NewGuid(), Email = $"capacity{i}@example.com", NormalizedEmail = $"CAPACITY{i}@EXAMPLE.COM",
                    UserName = $"capacity{i}", NormalizedUserName = $"CAPACITY{i}", IsActive = true, EmailConfirmed = true };
                db.Users.Add(user);
                db.TeamMemberships.Add(new() { TeamId = Team, UserId = user.Id });
            }
            if (code != "free") db.TeamPlanPurchases.Add(new()
            {
                TeamId = Team, UserId = Leader, PlanCode = code, AmountVnd = code == "premium20" ? 50000 : 110000,
                OrderCode = 12345, PaidAt = h.Clock.GetUtcNow()
            });
            await db.SaveChangesAsync(Ct);
        });
        var invite = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct));
        await Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct));
        var detail = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct));
        var request = Assert.Single(detail.JoinRequests!);
        var review = new TeamJoinReviewInput(request.Version, true);
        if (blocked)
        {
            Assert.Equal("TEAM_MEMBER_LIMIT", (await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamJoinRequestAsync(Leader, Team, Other, review, Ct)))).Code);
            Assert.Equal(activeCount, await h.DbAsync(db => db.TeamMemberships.CountAsync(x => x.TeamId == Team && x.Active, Ct)));
        }
        else
        {
            await Use(h, s => s.ReviewTeamJoinRequestAsync(Leader, Team, Other, review, Ct));
            Assert.Equal(activeCount + 1, await h.DbAsync(db => db.TeamMemberships.CountAsync(x => x.TeamId == Team && x.Active, Ct)));
        }
    }
}
