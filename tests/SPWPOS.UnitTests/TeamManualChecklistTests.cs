using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;

namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact]
    public async Task Manual_checklist_survives_posting_claiming_and_member_completion()
    {
        await using var h = await Setup();
        const string description = "TÓM TẮT\nBàn giao bản cuối.\n\nYÊU CẦU CẦN THỰC HIỆN\n- Đọc yêu cầu\n- Soát số liệu";
        var draft = await Use(h, s => s.DraftManualTeamTaskAsync(Leader, Team,
            new("Kiểm tra báo cáo", description, 60, Tomorrow), Ct));
        Assert.Equal(description, draft.Task!.Description);
        var id = await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct));
        var posted = (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(task => task.Id == id);
        Assert.Equal(new[] { "Đọc yêu cầu", "Soát số liệu" }, posted.Checklist!.Select(item => item.Text));

        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, id, Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        var personal = await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct));
        var checklist = JsonSerializer.Deserialize<TaskChecklistItem[]>(personal.ChecklistJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(posted.Checklist!.Select(item => item.Id), checklist.Select(item => item.Id));
        Assert.All(checklist, item => Assert.False(item.Completed));

        var assigned = (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(task => task.Id == id);
        await Use(h, s => s.SetTeamChecklistItemAsync(Member, id, checklist[0].Id, new(assigned.Version, true), Ct));
        var completed = (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Single(task => task.Id == id);
        Assert.True(completed.Checklist![0].Completed);
        Assert.False(completed.Checklist[1].Completed);
    }

    [Fact]
    public async Task Deleting_every_manual_checklist_item_keeps_the_summary_without_implicit_steps()
    {
        await using var h = await Setup();
        var draft = await Use(h, s => s.DraftManualTeamTaskAsync(Leader, Team,
            new("Báo cáo", "TÓM TẮT\nGiữ mô tả\n\nYÊU CẦU CẦN THỰC HIỆN\n- Mục cũ", 60, Tomorrow), Ct));
        var id = await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, draft.ProposalId, Ct));
        var original = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single(task => task.Id == id);
        Assert.Single(original.Checklist!);
        var change = await Use(h, s => s.ProposeManualTeamTaskChangeAsync(Leader, id,
            new(original.Version, new("Báo cáo", "## Tóm tắt\nGiữ mô tả\n\nYÊU CẦU CẦN THỰC HIỆN\n", 60, Tomorrow)), Ct));
        await Use(h, s => s.ConfirmTeamTaskDraftAsync(Leader, Team, change.ProposalId, Ct));
        var updated = (await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks.Single(task => task.Id == id);
        Assert.Empty(updated.Checklist!);
        Assert.Contains("Giữ mô tả", updated.Description);
        Assert.DoesNotContain("Mục cũ", updated.Description);
    }
}
