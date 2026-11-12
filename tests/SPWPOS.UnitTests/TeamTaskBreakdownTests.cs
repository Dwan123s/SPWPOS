using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;

namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact]
    public async Task Splitting_replaces_claimable_parent_with_two_independently_claimable_children()
    {
        await using var h = await Setup();
        var parent = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks);
        var preview = await Use(h, s => s.PreviewTeamClaimAsync(Member, parent.Id, Ct));
        var input = new TeamTaskSplitInput(parent.Version,
        [
            new("Thu dữ liệu", 30, ["Kiểm tra cảm biến", "Lưu dữ liệu"], "Dùng thiết bị trong phòng lab.", Member),
            new("Viết báo cáo", 30, ["Vẽ biểu đồ"], "Nộp bản PDF.")
        ]);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SplitTeamTaskAsync(Member, parent.Id, input, Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SplitTeamTaskAsync(Leader, parent.Id,
            input with { Children = [input.Children[0], input.Children[1] with { DesignatedUserId = Other }] }, Ct)));
        Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks);

        var ids = await Use(h, s => s.SplitTeamTaskAsync(Leader, parent.Id, input, Ct));
        Assert.Equal(2, ids.Length);
        var detail = await Use(h, s => s.GetTeamAsync(Member, Team, Ct));
        var container = detail.Tasks.Single(t => t.Id == parent.Id);
        Assert.Equal("Split", container.Status);
        Assert.Equal(2, container.SubtaskCount);
        var child = detail.Tasks.Single(t => t.Id == ids[0]);
        Assert.Equal(parent.Id, child.ParentTaskId);
        Assert.Equal(parent.Deadline, child.Deadline);
        Assert.Equal(new[] { "Kiểm tra cảm biến", "Lưu dữ liệu" }, child.Checklist!.Select(x => x.Text));
        Assert.Contains("Dùng thiết bị", child.Description);
        Assert.Null(detail.Tasks.Single(t => t.Id == ids[1]).DesignatedUserId);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamClaimAsync(Member, preview.Id, new(), Ct)));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.PreviewTeamClaimAsync(Leader, child.Id, Ct)));
        Assert.False(await h.DbAsync(db => db.WorkSessions.AnyAsync(Ct)));

        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, child.Id, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        detail = await Use(h, s => s.GetTeamAsync(Member, Team, Ct));
        child = detail.Tasks.Single(t => t.Id == child.Id);
        var item = child.Checklist![0];
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SetTeamChecklistItemAsync(Leader, child.Id, item.Id, new(child.Version, true), Ct)));
        await Use(h, s => s.SetTeamChecklistItemAsync(Member, child.Id, item.Id, new(child.Version, true), Ct));
        Assert.True((await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(t => t.Id == child.Id).Checklist![0].Completed);
        Assert.False((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single(t => t.Id == child.Id).Checklist![0].Completed);

        var personal = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        await using var scope = h.Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWorkspaceService>().UpdateTaskChecklistAsync(Member, personal.Id,
            new(personal.Version, child.Checklist.Select(step => step with { Completed = true }).ToArray()), Ct);
        Assert.All((await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(t => t.Id == child.Id).Checklist!, step => Assert.True(step.Completed));
    }
}
