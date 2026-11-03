using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    [Fact] public async Task Leader_can_attach_the_missing_original_to_an_existing_task_and_retry_without_duplicates()
    {
        await using var h = await Setup();
        var task = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks);
        var input = new TeamSourceFileInput(task.Version, Guid.NewGuid(), TextBrief("Đề bài cần gửi bổ sung"));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.AttachTeamSourceFileAsync(Member, task.Id, input, Ct)));
        await Use(h, s => s.AttachTeamSourceFileAsync(Leader, task.Id, input, Ct));
        await Use(h, s => s.AttachTeamSourceFileAsync(Leader, task.Id, input, Ct));
        Assert.Equal(1, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
        var shared = Assert.Single((await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks);
        Assert.Equal(input.RequestId, Assert.Single(shared.SourceFiles!).Id);
        var file = await Use(h, s => s.DownloadTeamSourceFileAsync(Member, shared.Id, input.RequestId, Ct));
        Assert.Equal(Convert.FromBase64String(input.File.Data), file.Content);
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        var reused = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.AttachTeamSourceFileAsync(Leader, task.Id, input with { File = TextBrief("Tài liệu khác") }, Ct)));
        Assert.Equal("REQUEST_REUSED", reused.Code);
        var replaced = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.AttachTeamSourceFileAsync(Leader, task.Id, input with { RequestId = Guid.NewGuid() }, Ct)));
        Assert.Equal("TEAM_SOURCE_EXISTS", replaced.Code);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Source_upload_rejects_invalid_files_or_stale_tasks_without_saving_a_document(bool corrupt)
    {
        await using var h = await Setup();
        var task = Assert.Single((await Use(h, s => s.GetTeamAsync(Leader, Team, Ct))).Tasks);
        var input = new TeamSourceFileInput(corrupt ? task.Version : Guid.NewGuid(), Guid.NewGuid(),
            corrupt ? TextBrief("Không phải PDF", "bad.pdf") : TextBrief("Tài liệu đúng"));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.AttachTeamSourceFileAsync(Leader, task.Id, input, Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
        Assert.Null((await h.DbAsync(db => db.TeamTasks.SingleAsync(Ct))).SourceDocumentId);
    }

    [Fact] public async Task Brief_publishes_one_original_document_shared_by_all_tasks_with_metadata_only_in_group_reads()
    {
        await using var h = await Setup(new AgentAiTestDouble { BriefResponse = CompleteBrief });
        var input = BriefInput with { File = TextBrief("Tài liệu gốc không bị thay đổi khi phân tích.", "folder/Đề bài.txt") };
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, input, Ct));
        Assert.Equal(0, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
        Assert.All((await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks, t => Assert.Empty(t.SourceFiles!));
        var published = await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct));
        await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct));
        Assert.Equal(1, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
        var detail = await Use(h, s => s.GetTeamAsync(Member, Team, Ct));
        var tasks = detail.Tasks.Where(t => published.TaskIds.Contains(t.Id)).ToArray();
        Assert.All(tasks, t => {
            var file = Assert.Single(t.SourceFiles!); Assert.Equal(draft.ProposalId, file.Id);
            Assert.Equal("Đề bài.txt", file.Name); Assert.Equal("text/plain", file.MimeType);
        });
        var download = await Use(h, s => s.DownloadTeamSourceFileAsync(Member, tasks[0].Id, draft.ProposalId, Ct));
        Assert.Equal(Convert.FromBase64String(input.File!.Data), download.Content);
        Assert.DoesNotContain(input.File.Data, JsonSerializer.Serialize(detail));
        Assert.DoesNotContain(input.File.Data, await h.DbAsync(async db => (await db.PrivatePlanningProposals.SingleAsync(Ct)).PayloadJson));
        var leaderCopy = await Use(h, s => s.DownloadTeamSourceFileAsync(Leader, tasks[1].Id, draft.ProposalId, Ct));
        Assert.Equal(download.Content, leaderCopy.Content);
    }

    [Theory] [InlineData("outsider")] [InlineData("removed")] [InlineData("inactive")] [InlineData("unverified")] [InlineData("wrongTask")] [InlineData("wrongFile")]
    public async Task Source_download_rechecks_membership_account_and_task_file_pair(string kind)
    {
        await using var h = await Setup(new AgentAiTestDouble { BriefResponse = CompleteBrief });
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct));
        var published = await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct));
        await h.DbAsync(async db => {
            if (kind == "removed") (await db.TeamMemberships.SingleAsync(m => m.UserId == Member, Ct)).Active = false;
            if (kind == "inactive") (await db.Users.SingleAsync(u => u.Id == Member, Ct)).IsActive = false;
            if (kind == "unverified") (await db.Users.SingleAsync(u => u.Id == Member, Ct)).EmailConfirmed = false;
            await db.SaveChangesAsync(Ct);
        });
        var error = await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DownloadTeamSourceFileAsync(
            kind == "outsider" ? Other : Member, kind == "wrongTask" ? TaskId : published.TaskIds[0], kind == "wrongFile" ? Guid.NewGuid() : draft.ProposalId, Ct)));
        Assert.Equal(kind is "wrongTask" or "wrongFile" ? "NOT_FOUND" : kind is "inactive" or "unverified" ? "FORBIDDEN" : "TEAM_FORBIDDEN", error.Code);
    }

    [Fact] public async Task Text_only_drafts_and_legacy_drafts_without_source_files_remain_publishable()
    {
        await using var h = await Setup(new AgentAiTestDouble { BriefResponse = CompleteBrief });
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, new("hạn ngày mai"), Ct));
        // Simulate a draft created by the earlier version, which had no SourceFile field.
        await h.DbAsync(async db => {
            var row = await db.PrivatePlanningProposals.SingleAsync(Ct);
            row.PayloadJson = row.PayloadJson.Replace(",\"SourceFile\":null", ""); await db.SaveChangesAsync(Ct);
        });
        var published = await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct));
        Assert.Equal(2, published.TaskIds.Length); Assert.Equal(0, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
    }

    [Fact] public async Task Expired_brief_cannot_publish_or_expose_its_source_document()
    {
        await using var h = await Setup(new AgentAiTestDouble { BriefResponse = CompleteBrief });
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct)); h.Clock.Advance(TimeSpan.FromMinutes(31));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct)));
        Assert.Equal(0, await h.DbAsync(db => db.TeamTaskDocuments.CountAsync(Ct)));
        Assert.Single((await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks);
    }
}
