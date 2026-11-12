using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Domain.Entities;

namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact]
    public async Task Team_workload_is_leader_only_and_counts_private_busy_time_without_returning_details()
    {
        await using var h = await Setup();
        h.Clock.Advance(TimeSpan.FromDays(1));
        await h.DbAsync(async db =>
        {
            db.FixedEvents.Add(new() { UserId = Member, Name = "Private appointment", Start = Tomorrow, End = Tomorrow.AddHours(3) });
            db.WorkTasks.Add(new() { UserId = Member, Name = "Private task", EstimatedMinutes = 60, Deadline = Tomorrow.AddHours(3) });
            await db.SaveChangesAsync(Ct);
        });

        var denied = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamWorkloadAsync(Member, Team, 0, Ct)));
        Assert.Equal("TEAM_FORBIDDEN", denied.Code);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.GetTeamWorkloadCandidatesAsync(Other, Team, TaskId, Ct)));
        var workload = await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 0, Ct));
        Assert.Equal(new DateOnly(2026, 11, 2), workload.Start);
        Assert.Equal(2, workload.Members.Length);
        var day = workload.Members.Single(m => m.UserId == Member).Days[0];
        Assert.Equal(180, day.WorkingMinutes);
        Assert.Equal(180, day.BusyMinutes);
        Assert.Equal(0, day.SchedulableMinutes);
        Assert.Equal("UNASSIGNED", Assert.Single(workload.Tasks).Risk);
        var json = JsonSerializer.Serialize(workload);
        Assert.DoesNotContain("Private appointment", json);
        Assert.DoesNotContain("Private task", json);
        Assert.DoesNotContain("PRIVATE_CLASS", json);
        Assert.Empty((await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 1, Ct))).Tasks);

        var candidates = await Use(h, s => s.GetTeamWorkloadCandidatesAsync(Leader, Team, TaskId, Ct));
        Assert.Equal(Leader, candidates[0].UserId);
        Assert.Equal("FITS", candidates[0].Status);
        Assert.Equal("NEEDS_REPLAN", candidates.Single(c => c.UserId == Member).Status);
        Assert.False(await h.DbAsync(db => db.TeamClaimProposals.AnyAsync(Ct)));
        Assert.False(await h.DbAsync(db => db.WorkSessions.AnyAsync(Ct)));
    }

    [Fact]
    public async Task Team_workload_distinguishes_forecast_shortfall_from_overdue()
    {
        await using var h = await Setup();
        h.Clock.Advance(TimeSpan.FromDays(1));
        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        Assert.Equal("ON_TRACK", Assert.Single((await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 0, Ct))).Tasks).Risk);

        await h.DbAsync(async db =>
        {
            db.FixedEvents.Add(new FixedEvent { UserId = Member, Name = "Private blocker", Start = Tomorrow, End = Tomorrow.AddHours(3) });
            await db.SaveChangesAsync(Ct);
        });
        var behind = Assert.Single((await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 0, Ct))).Tasks);
        Assert.Equal("BEHIND", behind.Risk);
        Assert.Equal(60, behind.ShortfallMinutes);
        h.Clock.Advance(TimeSpan.FromHours(16));
        var overdue = Assert.Single((await Use(h, s => s.GetTeamWorkloadAsync(Leader, Team, 0, Ct))).Tasks);
        Assert.Equal("OVERDUE", overdue.Risk);
        Assert.Null(overdue.ShortfallMinutes);
    }
}
