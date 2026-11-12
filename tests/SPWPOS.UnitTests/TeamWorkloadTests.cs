using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Each_member_preview_distinguishes_continuous_and_fragmented_hours(bool splittable)
    {
        await using var h = await Setup(hours: false);
        h.Clock.Advance(TimeSpan.FromDays(1));
        await h.DbAsync(async db =>
        {
            var task = await db.TeamTasks.SingleAsync(Ct);
            task.EstimatedMinutes = 90;
            task.Splittable = splittable;
            db.WeeklyAvailability.Add(new() { UserId = Leader, DayOfWeek = 1, StartMinute = 1140, EndMinute = 1260 });
            foreach (var start in new[] { 1140, 1185, 1230, 1275 })
                db.WeeklyAvailability.Add(new() { UserId = Member, DayOfWeek = 1, StartMinute = start, EndMinute = start + 30 });
            await db.SaveChangesAsync(Ct);
        });

        var continuous = await Use(h, s => s.PreviewTeamClaimAsync(Leader, TaskId, Ct));
        var fragmented = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        Assert.NotEmpty(continuous.Options);
        Assert.Equal(splittable, fragmented.Options.Length > 0);
        if (!splittable) Assert.Equal(120, fragmented.Failure?.FreeMinutes);
        Assert.False(await h.DbAsync(db => db.WorkSessions.AnyAsync(Ct)));
    }

    [Fact]
    public async Task Team_report_is_leader_only_and_contains_shared_task_progress_without_personal_capacity()
    {
        await using var h = await Setup();
        h.Clock.Advance(TimeSpan.FromDays(1));
        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        var denied = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamWorkloadAsync(Member, Team, 0, Ct)));
        Assert.Equal("TEAM_FORBIDDEN", denied.Code);

        var initial = await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 0, Ct));
        Assert.Equal(new DateOnly(2026, 11, 2), initial.Start);
        Assert.Equal(2, initial.Members.Length);
        var task = Assert.Single(initial.Tasks);
        Assert.Equal(Member, task.AssigneeId);
        Assert.Equal("ASSIGNED", task.Risk);

        await h.DbAsync(async db =>
        {
            db.FixedEvents.Add(new() { UserId = Member, Name = "Private blocker", Start = Tomorrow, End = Tomorrow.AddHours(3) });
            db.WorkTasks.Add(new() { UserId = Member, Name = "Private task", EstimatedMinutes = 60, Deadline = Tomorrow.AddHours(3) });
            await db.SaveChangesAsync(Ct);
        });
        var after = await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 0, Ct));
        var json = JsonSerializer.Serialize(after);
        Assert.Equal(JsonSerializer.Serialize(initial), json);
        foreach (var field in new[] { "Days", "TimeZoneId", "WorkingMinutes", "BusyMinutes", "SchedulableMinutes", "ShortfallMinutes", "Private blocker", "Private task" })
            Assert.DoesNotContain("\"" + field + "\"", json);
        Assert.Empty((await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 1, Ct))).Tasks);

        h.Clock.Advance(TimeSpan.FromHours(16));
        Assert.Equal("OVERDUE", Assert.Single((await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 0, Ct))).Tasks).Risk);
    }

    [Fact]
    public async Task Distant_work_can_be_posted_and_claim_preview_never_calls_the_model()
    {
        var aiCalls = 0;
        await using var h = await Setup(new AgentAiTestDouble
        {
            Response = _ => { aiCalls++; throw new InvalidOperationException("Claim preview should be local."); }
        });
        var deadline = Tomorrow.AddDays(40);
        var draft = await Use(h, s => s.DraftManualTeamTaskAsync(Leader, Team, new("Báo cáo", "Yêu cầu", 60, deadline), Ct));
        var id = await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct));
        var distant = await Use(h, s => s.PreviewTeamClaimAsync(Member, id, Ct));
        Assert.Empty(distant.Options);
        Assert.NotEmpty(distant.Warnings);
        Assert.NotEmpty((await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct))).Options);
        Assert.Equal(0, aiCalls);
        Assert.False(await h.DbAsync(db => db.WorkSessions.AnyAsync(Ct)));
    }

    [Fact]
    public async Task Claim_explanation_uses_verified_totals_and_requires_the_proposal_owner()
    {
        const string explanation = "Phương án dùng một phiên và giữ các phiên khác.";
        var ai = new AgentAiTestDouble { Response = _ => AgentAiTestDouble.Empty() with { Reply = explanation } };
        await using var h = await Setup(ai);
        var proposal = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        Assert.NotEmpty(proposal.Options);
        Assert.Equal(0, ai.Calls);
        var denied = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ExplainTeamClaimAsync(Leader, proposal.Id, Ct)));
        Assert.Equal("NOT_FOUND", denied.Code);
        Assert.Equal(explanation, await Use(h, s => s.ExplainTeamClaimAsync(Member, proposal.Id, Ct)));
        Assert.Equal(1, ai.Calls);
        using var document = JsonDocument.Parse(ai.Context);
        var option = document.RootElement.GetProperty("options")[0];
        Assert.Equal(60, option.GetProperty("plannedMinutes").GetInt32());
        Assert.False(option.TryGetProperty("start", out _));
        Assert.False(option.TryGetProperty("taskName", out _));
        Assert.False(await h.DbAsync(db => db.WorkSessions.AnyAsync(Ct)));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, proposal.Id, new(), Ct));
        var stale = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ExplainTeamClaimAsync(Member, proposal.Id, Ct)));
        Assert.Equal("CLAIM_STALE", stale.Code);
        Assert.Equal(1, ai.Calls);
    }
}
