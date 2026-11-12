using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Tomorrow = DateTimeOffset.Parse("2026-11-02T12:00:00Z");
    private static readonly Guid Leader = Guid.NewGuid(), Member = Guid.NewGuid(), Other = Guid.NewGuid(), Team = Guid.NewGuid(), TaskId = Guid.NewGuid();
    private static async Task<ServiceHarness> Setup(AgentAiTestDouble? ai = null, bool hours = true)
    {
        ai ??= new() { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Báo cáo chung", Tomorrow.AddHours(3), 60, 2, "OTHER", true)] } };
        var h = await ServiceHarness.CreateAsync(s => s.AddSingleton<ITaskInterpreter>(ai));
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-01T00:00:00Z") - h.Clock.GetUtcNow());
        await h.DbAsync(async db => {
            foreach (var uid in new[] { Leader, Member, Other })
            {
                db.Users.Add(new() { Id = uid, Email = uid + "@example.com", NormalizedEmail = (uid + "@example.com").ToUpperInvariant(), UserName = uid.ToString(), NormalizedUserName = uid.ToString().ToUpperInvariant(), FirstName = "Thành", LastName = "Viên", IsActive = true, EmailConfirmed = true });
                db.WorkPreferences.Add(new() { UserId = uid, TimeZoneId = "Asia/Ho_Chi_Minh", BreakMinutes = 0 });
                db.FixedEvents.Add(new() { UserId = uid, Name = "PRIVATE_CLASS_" + uid, Start = Tomorrow.AddHours(-11), End = Tomorrow.AddHours(-9) });
                if (hours) foreach (var day in Enumerable.Range(0, 7)) db.WeeklyAvailability.Add(new() { UserId = uid, DayOfWeek = day, StartMinute = 1140, EndMinute = 1320 });
            }
            db.TeamProjects.Add(new() { Id = Team, Name = "Đồ án", CreatorId = Leader });
            db.TeamMemberships.AddRange(new TeamMembership { TeamId = Team, UserId = Leader, Role = "Leader" }, new TeamMembership { TeamId = Team, UserId = Member });
            db.TeamTasks.Add(new() { Id = TaskId, TeamId = Team, Name = "Báo cáo chung", Description = "Công việc chung", EstimatedMinutes = 60, Deadline = Tomorrow.AddHours(3) });
            await db.SaveChangesAsync(Ct);
        });
        return h;
    }
    private static async Task<T> Use<T>(ServiceHarness h, Func<IPrivateTeamService, Task<T>> action) { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IPrivateTeamService>()); }
    private static Task Use(ServiceHarness h, Func<IPrivateTeamService, Task> action) => Use(h, async s => { await action(s); return true; });
    private static async Task<T> Workspace<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action) { await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>()); }
    [Fact] public async Task Preview_is_private_and_leaves_pool_and_calendar_untouched()
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        Assert.NotEmpty(preview.Options); Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        var shared = await Use(h, s => s.GetTeamAsync(Leader, Team, Ct)); Assert.Null(Assert.Single(shared.Tasks).AssigneeId);
        var json = JsonSerializer.Serialize(shared); Assert.DoesNotContain("PRIVATE_CLASS", json); Assert.DoesNotContain("WorkTaskId", json); Assert.DoesNotContain("Sessions", json); Assert.DoesNotContain("Availability", json);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamAsync(Other, Team, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Leader, preview.Id, new(), Ct)));
    }
    [Fact]
    public async Task Claim_is_feasible_with_work_hours_and_no_fixed_events()
    {
        await using var h = await Setup();
        await h.DbAsync(async db =>
        {
            db.FixedEvents.RemoveRange(await db.FixedEvents.Where(e => e.UserId == Member).ToArrayAsync(Ct));
            await db.SaveChangesAsync(Ct);
        });
        var preview = await Use(h, service => service.PreviewTeamClaimAsync(Member, TaskId, Ct));
        Assert.NotEmpty(preview.Options);
        await Use(h, service => service.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        Assert.Equal(1, await h.DbAsync(db => db.TeamTaskAssignments.CountAsync(Ct)));
    }

    [Fact] public async Task Missing_work_hours_is_an_onboarding_step_not_a_capacity_failure()
    {
        await using var h = await Setup(hours:false);
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        Assert.Empty(preview.Options); Assert.Empty(preview.Warnings);
        Assert.Equal("WORK_HOURS_NOT_CONFIRMED", preview.Failure!.Code); Assert.True(preview.Failure.NeedsAvailability); Assert.Equal(0, preview.Failure.MissingMinutes);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.AvailabilityPolicies.CountAsync(Ct)));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Infeasible_claim_explains_actual_capacity_or_session_limit(bool continuous)
    {
        await using var h = await Setup();
        await h.DbAsync(async db => { var task = await db.TeamTasks.SingleAsync(Ct); task.Splittable = !continuous; task.EstimatedMinutes = continuous?120:180; task.Deadline = DateTimeOffset.Parse("2026-11-01T14:00:00Z"); await db.SaveChangesAsync(Ct); });
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        Assert.Empty(preview.Options); Assert.NotNull(preview.Failure); Assert.False(preview.Failure.NeedsAvailability);
        Assert.Equal(120, preview.Failure.FreeMinutes); Assert.Equal(120, preview.Failure.CapacityMinutes);
        Assert.Equal(continuous?"CONTINUOUS_TASK_EXCEEDS_SESSION_LIMIT":"INSUFFICIENT_TIME_BEFORE_DEADLINE", preview.Failure.Code);
        Assert.Equal(continuous?0:60, preview.Failure.MissingMinutes); Assert.NotEmpty(preview.Failure.Blockers);
        Assert.Equal(0, await h.DbAsync(db=>db.WorkTasks.CountAsync(Ct)));
    }
    [Fact] public async Task Claim_is_idempotent_and_competing_preview_cannot_change_losers_calendar()
    {
        await using var h = await Setup(); var a = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); var b = await Use(h, s => s.PreviewTeamClaimAsync(Leader, TaskId, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, a.Id, new(), Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Member, a.Id, new(), Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Leader, b.Id, new(), Ct))); Assert.Equal("CLAIM_STALE", error.Code);
        Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(1, await h.DbAsync(db => db.TeamTaskAssignments.CountAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(x => x.UserId == Leader, Ct)));
        Assert.All(await h.DbAsync(db => db.WorkSessions.ToArrayAsync(Ct)), session => { Assert.Equal(Member, session.UserId); Assert.True(session.End <= Tomorrow.AddHours(3)); });
    }
    [Theory] [InlineData("calendar")] [InlineData("task")] [InlineData("membership")] [InlineData("expired")]
    public async Task Stale_or_expired_claim_rolls_back_everything(string kind)
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await h.DbAsync(async db => {
            if (kind == "calendar") (await db.WorkPreferences.SingleAsync(x => x.UserId == Member, Ct)).Revision = Guid.NewGuid();
            if (kind == "task") (await db.TeamTasks.SingleAsync(Ct)).Version = Guid.NewGuid();
            if (kind == "membership") (await db.TeamMemberships.SingleAsync(x => x.UserId == Member, Ct)).Version = Guid.NewGuid();
            await db.SaveChangesAsync(Ct);
        });
        if (kind == "expired") h.Clock.Advance(TimeSpan.FromMinutes(16));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct))); Assert.Null((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0].AssigneeId);
    }
    [Fact] public async Task Cancel_and_undo_release_task_and_restore_original_calendar()
    {
        await using var h = await Setup(); var cancelled = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); await Use(h, s => s.CancelTeamClaimAsync(Member, cancelled.Id, Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Member, cancelled.Id, new(), Ct)));
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct)); await Use(h, s => s.UndoTeamClaimAsync(Member, preview.Id, Ct));
        Assert.Null((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0].AssigneeId);
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(x => x.Status == WorkSessionStatus.Scheduled, Ct))); Assert.Equal(0, await h.DbAsync(db => db.TeamTaskAssignments.CountAsync(x => x.Active, Ct)));
    }
    [Fact] public async Task Progress_is_confirmed_by_assignee_and_return_is_atomic()
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        var task = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0];
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.UpdateTeamProgressAsync(Leader, TaskId, new(task.Version, 100), Ct)));
        await Use(h, s => s.UpdateTeamProgressAsync(Member, TaskId, new(task.Version, 50), Ct)); task = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0]; Assert.Equal(50, task.Progress);
        var member = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Members.Single(x => x.UserId == Member);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.RemoveTeamMemberAsync(Member, Team, Member, member.Version, Ct)));
        var returned = await Use(h, s => s.PreviewTeamReturnAsync(Member, TaskId, Ct));
        await Use(h, s => s.ReturnTeamTaskAsync(Member, TaskId, task.Version, Ct, returned.Revision)); Assert.Null((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0].AssigneeId);
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(x => x.Status == WorkSessionStatus.Scheduled, Ct)));
    }
    [Fact] public async Task Personal_edit_cannot_diverge_from_shared_contract_and_done_updates_group()
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        var personal = await h.DbAsync(db => db.WorkTasks.AsNoTracking().SingleAsync(Ct));
        await Assert.ThrowsAsync<AuthException>(() => Workspace(h, s => s.SaveTaskAsync(Member, personal.Id, new(personal.Name, personal.Description, personal.Deadline!.Value.AddDays(1), personal.EstimatedMinutes, Version: personal.Version), Ct)));
        await using var scope = h.Provider.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IWorkspaceService>().SetStatusAsync(Member, personal.Id, new("Completed", 100), Ct);
        Assert.Equal("PendingReview", (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0].Status);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Leader_change_keeps_effective_deadline_until_private_acceptance(bool manual)
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        var original = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single();
        var input = new ManualTeamTaskChangeInput(original.Version,
            new("Báo cáo cập nhật", "", 60, original.Deadline.AddDays(1)));
        if (manual)
        {
            await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ProposeManualTeamTaskChangeAsync(Member, TaskId, input, Ct)));
            await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ProposeManualTeamTaskChangeAsync(Leader, TaskId, input with { Version = Guid.NewGuid() }, Ct)));
        }
        var draft = manual
            ? await Use(h, s => s.ProposeManualTeamTaskChangeAsync(Leader, TaskId, input, Ct))
            : await Use(h, s => s.ProposeTeamTaskChangeAsync(Leader, TaskId, new("Đổi báo cáo"), Ct));
        Assert.NotNull(draft.Task);
        await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct));
        var pending = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single();
        Assert.True(pending.HasPendingChange);
        Assert.Equal(original.Deadline, pending.Deadline);
        var change = await Use(h, s => s.PreviewTeamTaskChangeAsync(Member, TaskId, Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Member, change.Id, new(), Ct));
        Assert.False((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0].HasPendingChange); Assert.Equal(1, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        Assert.Equal(draft.Task.Deadline, (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).Deadline);
    }
    [Fact] public async Task Invite_hashes_codes_revocation_and_role_checks_are_enforced()
    {
        await using var h = await Setup(); await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.CreateTeamInviteAsync(Member, Team, Ct)));
        var invite = await Use(h, s => s.CreateTeamInviteAsync(Leader, Team, Ct)); Assert.NotEqual(invite.Code, await h.DbAsync(db => db.TeamInvites.Select(x => x.CodeHash).SingleAsync(Ct)));
        Assert.Equal(Team, await Use(h, s => s.JoinTeamAsync(Other, invite.Code, Ct))); await Use(h, s => s.RevokeTeamInviteAsync(Leader, Team, invite.Id, Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.JoinTeamAsync(Member, invite.Code, Ct)));
    }
    [Fact] public async Task Shared_AI_context_excludes_personal_data_and_draft_needs_confirmation()
    {
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Tasks = [new(null, "Task chung", Tomorrow, 90, 2, "OTHER", true, DeadlineEvidence: "hạn ngày mai")] } };
        await using var h = await Setup(ai); var draft = await Use(h, s => s.DraftTeamTaskAsync(Leader, Team, new("Task chung hạn ngày mai"), Ct));
        Assert.DoesNotContain("PRIVATE_CLASS", ai.Context); Assert.DoesNotContain("availability", ai.Context); Assert.DoesNotContain("sessions", ai.Context); Assert.Equal(1, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct)));
        await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct)); await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct)); Assert.Equal(2, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct)));
    }
    [Fact] public async Task Policy_is_only_real_after_confirmation_and_preserves_source_hours()
    {
        await using var h = await Setup(hours: false); Assert.Null(await Use(h, s => s.GetAvailabilityPolicyAsync(Member, Ct)));
        var preview = await Use(h, s => s.PreviewAvailabilityPolicyAsync(Member, Ct)); Assert.Equal(0, await h.DbAsync(db => db.AvailabilityPolicies.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.WeeklyAvailability.CountAsync(Ct)));
        await Use(h, s => s.ConfirmAvailabilityPolicyAsync(Member, preview.Id, Ct)); Assert.True((await Use(h, s => s.GetAvailabilityPolicyAsync(Member, Ct)))!.Confirmed); Assert.Equal(0, await h.DbAsync(db => db.WeeklyAvailability.CountAsync(Ct)));
        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); Assert.NotEmpty(claim.Options);
    }
    [Fact] public async Task Failure_after_acquiring_shared_task_rolls_back_claim_and_calendar()
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        var first = preview.Options[0].After.First();
        h.Clock.Advance(first.Start.AddMinutes(1) - h.Clock.GetUtcNow());
        // Keep the proposal live to exercise the deeper calendar validation rollback.
        await h.DbAsync(async db => { (await db.TeamClaimProposals.SingleAsync(Ct)).ExpiresAt = h.Clock.GetUtcNow().AddMinutes(5); await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.TeamTaskAssignments.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        Assert.Null((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0].AssigneeId);
    }
    [Fact] public async Task Completing_a_session_does_not_complete_shared_task()
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        var session = await h.DbAsync(db => db.WorkSessions.SingleAsync(Ct)); h.Clock.Advance(session.Start - h.Clock.GetUtcNow());
        await using var scope = h.Provider.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>(); await service.ExecuteSessionAsync(Member, session.Id, "start", Ct); h.Clock.Advance(TimeSpan.FromMinutes(60)); await service.ExecuteSessionAsync(Member, session.Id, "complete", Ct);
        var task = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks[0]; Assert.Equal(0, task.Progress); Assert.Equal("Assigned", task.Status);
    }
    [Fact] public async Task Shared_chat_is_members_only_idempotent_and_does_not_stale_a_claim()
    {
        await using var h = await Setup(); var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); var request = new TeamChatInput(Guid.NewGuid(), "Mình nhận phần báo cáo nhé.");
        var sent = await Use(h, s => s.SendTeamChatAsync(Member, Team, request, Ct)); var retry = await Use(h, s => s.SendTeamChatAsync(Member, Team, request, Ct)); Assert.Equal(sent.Id, retry.Id);
        var history = await Use(h, s => s.GetTeamChatAsync(Leader, Team, null, null, Ct)); Assert.Equal(request.Text, Assert.Single(history.Items).Text);
        Assert.Empty((await Use(h, s => s.GetTeamChatAsync(Leader, Team, sent.Id, null, Ct))).Items);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamChatAsync(Other, Team, null, null, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SendTeamChatAsync(Other, Team, new(Guid.NewGuid(), "Không thuộc nhóm"), Ct)));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct)); Assert.Equal(1, await h.DbAsync(db => db.TeamChatMessages.CountAsync(Ct)));
    }
    [Fact] public async Task Shared_chat_pagination_is_stable_when_messages_share_a_timestamp()
    {
        await using var h = await Setup();
        await h.DbAsync(async db => { foreach (var i in Enumerable.Range(1, 105)) db.TeamChatMessages.Add(new() { TeamId = Team, UserId = Member, RequestId = Guid.NewGuid(), Text = "Tin " + i, CreatedAt = h.Clock.GetUtcNow() }); await db.SaveChangesAsync(Ct); });
        var latest = await Use(h, s => s.GetTeamChatAsync(Member, Team, null, null, Ct)); Assert.Equal(50, latest.Items.Length); Assert.True(latest.HasMore); Assert.Equal("Tin 105", latest.Items[^1].Text);
        var previous = await Use(h, s => s.GetTeamChatAsync(Member, Team, null, latest.Items[0].Id, Ct)); Assert.Equal(50, previous.Items.Length); Assert.True(previous.HasMore); Assert.True(previous.Items[^1].Id < latest.Items[0].Id);
        var rest = await Use(h, s => s.GetTeamChatAsync(Member, Team, null, previous.Items[0].Id, Ct)); Assert.Equal(5, rest.Items.Length); Assert.False(rest.HasMore);
        var member = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Members.Single(x => x.UserId == Member); await Use(h, s => s.RemoveTeamMemberAsync(Leader, Team, Member, member.Version, Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamChatAsync(Member, Team, null, null, Ct)));
    }
    [Fact] public async Task A_240_minute_claim_respects_account_session_limits()
    {
        await using var h = await Setup(); await h.DbAsync(async db => { (await db.TeamTasks.SingleAsync(Ct)).EstimatedMinutes = 240; await db.SaveChangesAsync(Ct); });
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); Assert.NotEmpty(preview.Options);
        Assert.Equal(240, preview.Options[0].After.Sum(x => (x.End - x.Start).TotalMinutes)); Assert.All(preview.Options[0].After, x => Assert.True((x.End - x.Start).TotalMinutes <= 90));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Negotiation_moves_only_flexible_lower_priority_work_and_keeps_its_deadline(bool locked)
    {
        await using var h = await Setup();
        var now = h.Clock.GetUtcNow(); var slot = DateTimeOffset.Parse("2026-11-01T12:00:00Z");
        await h.DbAsync(async db => {
            var shared = await db.TeamTasks.SingleAsync(Ct); shared.Deadline = slot.AddHours(1); shared.Priority = 4;
            var personal = new WorkTask { UserId = Member, Name = "PRIVATE_OLD_TASK", Deadline = Tomorrow.AddHours(2), EstimatedMinutes = 180, Priority = 1, IsHardDeadline = true }; db.WorkTasks.Add(personal);
            db.WorkSessions.AddRange(new WorkSession { UserId = Member, TaskId = personal.Id, Start = slot, End = slot.AddMinutes(90), IsLocked = locked }, new WorkSession { UserId = Member, TaskId = personal.Id, Start = slot.AddMinutes(90), End = slot.AddHours(3), IsLocked = locked }); await db.SaveChangesAsync(Ct);
        });
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        if (locked) { Assert.Empty(preview.Options); return; }
        Assert.NotEmpty(preview.Options); Assert.Contains(preview.Options[0].After, x => x.TaskName == "PRIVATE_OLD_TASK" && !x.Preserved);
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct));
        var sessions = await h.DbAsync(db => db.WorkSessions.Where(x => x.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct));
        Assert.All(sessions, x => Assert.True(x.End <= (x.Start >= Tomorrow ? Tomorrow.AddHours(2) : slot.AddHours(3))));
        Assert.DoesNotContain("PRIVATE_OLD_TASK", JsonSerializer.Serialize(await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))));
    }
    [Fact] public async Task Two_members_with_the_same_deadline_receive_different_personal_schedules()
    {
        await using var h = await Setup(); var secondId = Guid.NewGuid();
        await h.DbAsync(async db => {
            db.TeamTasks.Add(new() { Id = secondId, TeamId = Team, Name = "Task thứ hai", EstimatedMinutes = 60, Deadline = Tomorrow.AddHours(3) });
            db.WeeklyAvailability.RemoveRange(await db.WeeklyAvailability.Where(x => x.UserId == Leader).ToArrayAsync(Ct));
            foreach (var day in Enumerable.Range(0,7)) db.WeeklyAvailability.Add(new() { UserId = Leader, DayOfWeek = day, StartMinute = 480, EndMinute = 720 });
            await db.SaveChangesAsync(Ct);
        });
        var a = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct)); var b = await Use(h, s => s.PreviewTeamClaimAsync(Leader, secondId, Ct));
        Assert.NotEqual(a.Options[0].After[0].Start, b.Options[0].After[0].Start);
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, a.Id, new(), Ct)); await Use(h, s => s.ConfirmTeamClaimAsync(Leader, b.Id, new(), Ct));
        var first = await Workspace(h, s => s.GetAsync(Member, Ct)); var second = await Workspace(h, s => s.GetAsync(Leader, Ct));
        Assert.Equal("Báo cáo chung", Assert.Single(first.Tasks).Name); Assert.Equal("Task thứ hai", Assert.Single(second.Tasks).Name);
        Assert.Equal(first.Tasks[0].Deadline, second.Tasks[0].Deadline); Assert.NotEqual(first.Sessions[0].Start, second.Sessions[0].Start);
    }
}
