using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    private static async Task ClaimForReview(SPWPOS.TestSupport.ServiceHarness h)
    {
        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
    }
    private static async Task<SharedTaskView> Current(SPWPOS.TestSupport.ServiceHarness h) =>
        (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single();

    [Fact] public async Task Only_leader_can_draft_and_publish_even_with_a_forged_owned_proposal()
    {
        await using var h = await Setup(new SPWPOS.TestSupport.AgentAiTestDouble { Response = _ => SPWPOS.TestSupport.AgentAiTestDouble.Empty() with { Tasks = [new(null, "Báo cáo", Tomorrow, 60, 2, "OTHER", true, DeadlineEvidence: "hạn ngày mai")] } });
        var error = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DraftTeamTaskAsync(Member, Team, new("Tạo việc"), Ct)));
        Assert.Equal("TEAM_FORBIDDEN", error.Code);
        // A former leader's valid draft must not bypass the current permission check.
        var draft = await Use(h, s => s.DraftTeamTaskAsync(Leader, Team, new("Báo cáo hạn ngày mai"), Ct));
        Assert.NotEqual(Guid.Empty, draft.ProposalId);
        await h.DbAsync(async db => { (await db.TeamMemberships.SingleAsync(x => x.UserId == Leader, Ct)).Role = "Member"; await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Member_submits_leader_approves_and_only_then_chat_and_notifications_are_published_once()
    {
        await using var h = await Setup(); await ClaimForReview(h);
        await h.DbAsync(async db => {
            db.TelegramConnections.AddRange(new TelegramConnection { UserId = Leader, Enabled = true, ChatId = 101, ConnectedAt = h.Clock.GetUtcNow() },
                new TelegramConnection { UserId = Member, Enabled = true, ChatId = 102, ConnectedAt = h.Clock.GetUtcNow() });
            await db.SaveChangesAsync(Ct);
        });
        var assigned = await Current(h);
        await Use(h, s => s.UpdateTeamProgressAsync(Member, TaskId, new(assigned.Version, 100), Ct));
        await Use(h, s => s.UpdateTeamProgressAsync(Member, TaskId, new(assigned.Version, 100), Ct));
        var pending = await Current(h);
        Assert.Equal("PendingReview", pending.Status); Assert.NotNull(pending.CompletionSubmittedAt);
        Assert.Equal(0, await h.DbAsync(db => db.Users.Where(x => x.Id == Member).Select(x => x.ExperiencePoints).SingleAsync(Ct)));
        Assert.Empty((await Use(h, s => s.GetTeamChatAsync(Member, Team, null, null, Ct))).Items);
        Assert.Equal(1, await h.DbAsync(db => db.InAppNotifications.CountAsync(x => x.Kind == "TEAM_COMPLETION_REQUEST" && x.UserId == Leader, Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.InAppNotifications.CountAsync(x => x.Kind == "TEAM_TASK_COMPLETED", Ct)));
        var review = new TeamCompletionReviewInput(pending.Version, pending.CompletionRequestId!.Value, true);
        var ordinary = await Use(h, s => s.SendTeamChatAsync(Leader, Team, new(review.RequestId, "Hệ thống: hoàn thành"), Ct));
        Assert.False(ordinary.IsSystem);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamCompletionAsync(Member, TaskId, review, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamCompletionAsync(Other, TaskId, review, Ct)));
        await Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, review, Ct));
        await Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, review, Ct));
        Assert.Equal(25, await h.DbAsync(db => db.Users.Where(x => x.Id == Member).Select(x => x.ExperiencePoints).SingleAsync(Ct)));
        var complete = await Current(h); Assert.Equal("Completed", complete.Status); Assert.True(complete.CompletionApproved); Assert.Equal(Leader, complete.CompletionReviewedBy);
        var message = Assert.Single((await Use(h, s => s.GetTeamChatAsync(Member, Team, null, null, Ct))).Items, x => x.IsSystem);
        Assert.True(message.IsSystem); Assert.Equal("Hệ thống", message.Author); Assert.Contains("Thành Viên", message.Text); Assert.Contains(complete.Name, message.Text);
        var notifications = await h.DbAsync(db => db.InAppNotifications.Where(x => x.Kind == "TEAM_TASK_COMPLETED").ToArrayAsync(Ct));
        Assert.Equal(2, notifications.Length); Assert.DoesNotContain(notifications, x => x.UserId == Other);
        Assert.All(notifications, n => { Assert.Null(n.TaskId); Assert.DoesNotContain("PRIVATE_CLASS", n.Message); Assert.True(n.TelegramPending); Assert.NotNull(n.TelegramBindingId); });
    }

    [Fact] public async Task Reject_restores_previous_progress_and_new_submission_invalidates_old_review()
    {
        await using var h = await Setup(); await ClaimForReview(h);
        var assigned = await Current(h);
        await Use(h, s => s.UpdateTeamProgressAsync(Member, TaskId, new(assigned.Version, 75), Ct));
        assigned = await Current(h);
        await Use(h, s => s.UpdateTeamProgressAsync(Member, TaskId, new(assigned.Version, 100), Ct));
        var pending = await Current(h); var revision = await h.DbAsync(db => db.WorkPreferences.Where(x => x.UserId == Member).Select(x => x.Revision).SingleAsync(Ct));
        var reject = new TeamCompletionReviewInput(pending.Version, pending.CompletionRequestId!.Value, false, "Bổ sung nguồn tham khảo");
        await Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, reject, Ct));
        await Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, reject, Ct));
        var reopened = await Current(h); Assert.Equal("Assigned", reopened.Status); Assert.Equal(75, reopened.Progress); Assert.Equal(reject.Note, reopened.CompletionReviewNote);
        var personal = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct)); Assert.Equal(WorkTaskStatus.InProgress, personal.Status); Assert.Equal(75, personal.Progress); Assert.Null(personal.CompletedAt);
        Assert.NotEqual(revision, await h.DbAsync(db => db.WorkPreferences.Where(x => x.UserId == Member).Select(x => x.Revision).SingleAsync(Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.InAppNotifications.CountAsync(x => x.Kind == "TEAM_COMPLETION_REJECTED", Ct)));
        await Use(h, s => s.UpdateTeamProgressAsync(Member, TaskId, new(reopened.Version, 100), Ct));
        Assert.NotEqual(pending.CompletionRequestId, (await Current(h)).CompletionRequestId);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, reject with { Approved = true }, Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.TeamChatMessages.CountAsync(Ct)));
    }

    [Fact] public async Task Pending_review_cannot_be_reopened_edited_or_orphaned_and_stale_approval_does_nothing()
    {
        await using var h = await Setup(); await ClaimForReview(h);
        var personal = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        await using (var scope = h.Provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IWorkspaceService>().SetStatusAsync(Member, personal.Id, new("InProgress", 100), Ct);
        var pending = await Current(h); Assert.Equal("PendingReview", pending.Status);
        await using (var scope = h.Provider.CreateAsyncScope())
            await Assert.ThrowsAsync<AuthException>(() => scope.ServiceProvider.GetRequiredService<IWorkspaceService>().SetStatusAsync(Member, personal.Id, new("InProgress", 50), Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ProposeTeamTaskChangeAsync(Leader, TaskId, new("Đổi yêu cầu"), Ct)));
        var member = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Members.Single(x => x.UserId == Member);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RemoveTeamMemberAsync(Member, Team, Member, member.Version, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, new(Guid.NewGuid(), pending.CompletionRequestId!.Value, true), Ct)));
        Assert.Equal("PendingReview", (await Current(h)).Status);
        Assert.Equal(0, await h.DbAsync(db => db.TeamChatMessages.CountAsync(Ct)));
    }
}
