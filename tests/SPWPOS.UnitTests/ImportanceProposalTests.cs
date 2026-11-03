using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Importance_proposal_is_advisory_cached_and_only_confirmation_updates_task()
    {
        var ai = new AgentAiTestDouble { ImportanceResponse = c => new("Important", "Đầu ra bắt buộc của proposal", .6, c.Name) };
        await using var h = await Setup(ai);
        var task = new WorkTask { UserId = Member, Name = "Nghiệm thu proposal", Description = "Hợp nhất và kiểm tra đầu ra bắt buộc", Deadline = Tomorrow, EstimatedMinutes = 60 };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); await db.SaveChangesAsync(Ct); });
        var revision = await h.DbAsync(db => db.WorkPreferences.Where(p => p.UserId == Member).Select(p => p.Revision).SingleAsync(Ct));
        var beforeEvents = await h.DbAsync(db => db.FixedEvents.CountAsync(Ct));
        var proposal = await Eisenhower(h, s => s.ProposeImportanceAsync(Member, task.Id, false, new(task.Version), Ct));
        Assert.Equal("Important", proposal.Suggestion.Level); Assert.Equal(.6, proposal.Suggestion.Confidence);
        Assert.Equal(task.Version, proposal.Version);
        Assert.Equal(proposal, await Eisenhower(h, s => s.ProposeImportanceAsync(Member, task.Id, false, new(task.Version), Ct)));
        Assert.Equal(1, ai.Calls); Assert.Equal(task.Description, ai.ImportanceContext!.Description);
        var unchanged = await h.DbAsync(db => db.WorkTasks.SingleAsync(t => t.Id == task.Id, Ct));
        Assert.Equal("Unknown", unchanged.ImportanceLevel); Assert.Equal(task.Version, unchanged.Version);
        Assert.Equal(revision, await h.DbAsync(db => db.WorkPreferences.Where(p => p.UserId == Member).Select(p => p.Revision).SingleAsync(Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
        Assert.Equal(beforeEvents, await h.DbAsync(db => db.FixedEvents.CountAsync(Ct)));
        await Eisenhower(h, async s => { await s.SetImportanceAsync(Member, task.Id, false, new(proposal.Version, "Lower", "Tôi chỉ phụ trách phần hỗ trợ"), Ct); return true; });
        var saved = await h.DbAsync(db => db.WorkTasks.SingleAsync(t => t.Id == task.Id, Ct));
        Assert.Equal("Lower", saved.ImportanceLevel); Assert.Equal("User", saved.ImportanceSource); Assert.NotNull(saved.ImportanceConfirmedAt);
        Assert.NotEqual(task.Version, saved.Version); Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(Ct)));
    }
    [Fact] public async Task Importance_proposal_enforces_ownership_and_leader_role_before_calling_ai()
    {
        var ai = new AgentAiTestDouble { ImportanceResponse = c => new("Important", "Đầu ra chung", .9, c.Name) };
        await using var h = await Setup(ai);
        var task = new WorkTask { UserId = Member, Name = "Riêng tư", Deadline = Tomorrow, EstimatedMinutes = 25 };
        await h.DbAsync(async db => { db.WorkTasks.Add(task); await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Eisenhower(h, s => s.ProposeImportanceAsync(Leader, task.Id, false, new(task.Version), Ct)));
        var shared = await h.DbAsync(db => db.TeamTasks.SingleAsync(Ct));
        await Assert.ThrowsAsync<AuthException>(() => Eisenhower(h, s => s.ProposeImportanceAsync(Member, TaskId, true, new(shared.Version), Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Eisenhower(h, s => s.ProposeImportanceAsync(Other, TaskId, true, new(shared.Version), Ct)));
        Assert.Equal(0, ai.Calls);
        var proposal = await Eisenhower(h, s => s.ProposeImportanceAsync(Leader, TaskId, true, new(shared.Version), Ct));
        Assert.Equal("Important", proposal.Suggestion.Level); Assert.Empty(ai.ImportanceContext!.Dependencies);
        Assert.Equal(shared.Name, ai.ImportanceContext.Name); Assert.Equal(shared.Description, ai.ImportanceContext.Description);
        Assert.Equal("Unknown", (await h.DbAsync(db => db.TeamTasks.SingleAsync(Ct))).ImportanceLevel);
    }
    [Theory] [InlineData("version")] [InlineData("role")]
    public async Task Importance_proposal_rechecks_task_and_permissions_after_ai(string change)
    {
        var ai = new AgentAiTestDouble { ImportanceResponse = c => new("Important", "Đầu ra chung", .9, c.Name) };
        await using var h = await Setup(ai);
        var shared = await h.DbAsync(db => db.TeamTasks.SingleAsync(Ct));
        ai.BeforeReply = () => h.DbAsync(async db => {
            if (change == "version") (await db.TeamTasks.SingleAsync(Ct)).Version = Guid.NewGuid();
            else (await db.TeamMemberships.SingleAsync(m => m.UserId == Leader, Ct)).Role = "Member";
            await db.SaveChangesAsync(Ct);
        });
        await Assert.ThrowsAsync<AuthException>(() => Eisenhower(h, s => s.ProposeImportanceAsync(Leader, TaskId, true, new(shared.Version), Ct)));
        Assert.Equal("Unknown", (await h.DbAsync(db => db.TeamTasks.SingleAsync(Ct))).ImportanceLevel);
    }
    [Fact] public async Task Importance_proposal_rejects_fabricated_evidence_and_accepts_uncertain_unknown_without_saving()
    {
        var ai = new AgentAiTestDouble { ImportanceResponse = _ => new("Important", "Ảnh hưởng điểm môn", .95, "Không có trong dữ liệu") };
        await using var h = await Setup(ai);
        var shared = await h.DbAsync(db => db.TeamTasks.SingleAsync(Ct));
        var error = await Assert.ThrowsAsync<AuthException>(() => Eisenhower(h, s => s.ProposeImportanceAsync(Leader, TaskId, true, new(shared.Version), Ct)));
        Assert.Equal("IMPORTANCE_AI_INVALID", error.Code);
        ai.ImportanceResponse = _ => new("Unknown", "Cần biết đầu ra bắt buộc của công việc", .3, "");
        var value = await Eisenhower(h, s => s.ProposeImportanceAsync(Leader, TaskId, true, new(shared.Version), Ct));
        Assert.Equal("Unknown", value.Suggestion.Level); Assert.Equal(2, ai.Calls);
        Assert.Equal(shared.Version, (await h.DbAsync(db => db.TeamTasks.SingleAsync(Ct))).Version);
    }
}
