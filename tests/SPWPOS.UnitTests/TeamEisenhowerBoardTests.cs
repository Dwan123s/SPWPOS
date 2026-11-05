using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Member_checklist_and_quadrant_are_private_and_do_not_change_shared_contract_or_review_workflow()
    {
        await using var h = await Setup();
        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, TaskId, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        var task = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        var view = await Eisenhower(h, s => s.GetEisenhowerAsync(Member, Ct));
        var checklist = new TaskChecklistItem[] { new(Guid.NewGuid(), "Ghi chú riêng của thành viên", true) };
        await Eisenhower(h, async s => { await s.UpdateMatrixChecklistAsync(Member, task.Id, new(task.Version, checklist), Ct); return true; });
        var updated = await Eisenhower(h, s => s.GetEisenhowerAsync(Member, Ct)); var row = Assert.Single(updated.Tasks);
        Assert.Equal("Accepted", row.Kind); Assert.False(row.CanEdit); Assert.Equal(checklist, row.Checklist);
        await Eisenhower(h, async s => { await s.MoveMatrixTaskAsync(Member,task.Id,new(row.Version,updated.Revision,"Q1"),Ct); return true; });
        var shared = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader,Team,Ct))).Tasks);
        Assert.Equal("Assigned", shared.Status); Assert.Equal(0, shared.Progress); Assert.Equal(60, shared.EstimatedMinutes);
        Assert.DoesNotContain("Ghi chú riêng", System.Text.Json.JsonSerializer.Serialize(shared));
        var personal = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        Assert.Equal(0, personal.Progress); Assert.Equal(WorkTaskStatus.Todo, personal.Status);
        Assert.Equal("TEAM_CONTRACT_PROTECTED", (await Assert.ThrowsAsync<AuthException>(() => Eisenhower(h,
            s => s.SaveMatrixTaskAsync(Member, personal.Id, new("Đổi yêu cầu", "", personal.Deadline, 120, "Q1", personal.Version), Ct)))).Code);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => Eisenhower(h, async s => {
            await s.UpdateMatrixChecklistAsync(Leader,personal.Id,new(personal.Version,[]),Ct); return true;
        }))).Code);
        Assert.Equal(0, await h.DbAsync(db => db.TeamCompletionSubmissions.CountAsync(Ct)));
    }
}
