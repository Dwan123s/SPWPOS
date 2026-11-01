using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    private static TeamCompletionFileInput ReportFile(string name = "Báo cáo.txt", string content = "Kết quả đã kiểm tra") => new(name, Convert.ToBase64String(Encoding.UTF8.GetBytes(content)));

    [Fact] public async Task Report_upload_is_atomic_idempotent_and_private_to_assignee_and_leader()
    {
        await using var h = await Setup(); await ClaimForReview(h);
        await h.DbAsync(async db => { db.TeamMemberships.Add(new() { TeamId = Team, UserId = Other }); await db.SaveChangesAsync(Ct); });
        var assigned = await Current(h); var input = new TeamCompletionSubmitInput(assigned.Version, Guid.NewGuid(), "Ghi chú báo cáo riêng", [ReportFile()]);
        await Use(h, s => s.SubmitTeamCompletionAsync(Member, TaskId, input, Ct));
        await Use(h, s => s.SubmitTeamCompletionAsync(Member, TaskId, input, Ct));
        Assert.Equal(1, await h.DbAsync(db => db.TeamCompletionSubmissions.CountAsync(Ct))); Assert.Equal(1, await h.DbAsync(db => db.TeamCompletionFiles.CountAsync(Ct)));
        var pending = await Current(h); Assert.Equal("PendingReview", pending.Status); Assert.Equal(input.RequestId, pending.CompletionRequestId);
        Assert.Equal(input.Note, pending.CompletionSubmissionNote); var file = Assert.Single(pending.CompletionFiles!);
        Assert.Equal("Báo cáo.txt", file.Name); Assert.Equal("text/plain", file.MimeType);
        var json = JsonSerializer.Serialize((await Use(h, s => s.GetTeamAsync(Other, Team, Ct))).Tasks);
        Assert.DoesNotContain(input.Note!, json); Assert.DoesNotContain(file.Name, json); Assert.DoesNotContain(input.Files![0].Data, json);
        foreach (var owner in new[] { Leader, Member })
        {
            var download = await Use(h, s => s.DownloadTeamCompletionFileAsync(owner, TaskId, file.Id, Ct));
            Assert.Equal("Kết quả đã kiểm tra", Encoding.UTF8.GetString(download.Content)); Assert.Equal(file.Name, download.Name);
        }
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DownloadTeamCompletionFileAsync(Other, TaskId, file.Id, Ct)));
        await Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, new(pending.Version, input.RequestId, true), Ct));
        await Use(h, s => s.SubmitTeamCompletionAsync(Member, TaskId, input, Ct));
        Assert.Equal(1, await h.DbAsync(db => db.TeamCompletionFiles.CountAsync(Ct))); Assert.Equal("Completed", (await Current(h)).Status);
    }

    [Theory] [InlineData("wrongType")] [InlineData("corrupt")] [InlineData("tooMany")] [InlineData("empty")] [InlineData("oversize")] [InlineData("stale")] [InlineData("notAssignee")]
    public async Task Invalid_report_does_not_change_task_or_save_any_file(string reason)
    {
        await using var h = await Setup(); await ClaimForReview(h); var assigned = await Current(h);
        var files = reason switch {
            "wrongType" => new[] { ReportFile("program.exe") },
            "corrupt" => new[] { ReportFile("report.pdf", "not a PDF") },
            "tooMany" => Enumerable.Range(0, 4).Select(i => ReportFile(i + ".txt")).ToArray(),
            "empty" => new[] { new TeamCompletionFileInput("empty.txt", "") },
            "oversize" => new[] { new TeamCompletionFileInput("big.txt", new string('A', 11184816)) },
            _ => new[] { ReportFile() }
        };
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SubmitTeamCompletionAsync(reason == "notAssignee" ? Leader : Member, TaskId,
            new(reason == "stale" ? Guid.NewGuid() : assigned.Version, Guid.NewGuid(), Files: files), Ct)));
        Assert.Equal("Assigned", (await Current(h)).Status); Assert.Equal(assigned.Version, (await Current(h)).Version);
        Assert.Equal(0, await h.DbAsync(db => db.TeamCompletionFiles.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.TeamCompletionSubmissions.CountAsync(Ct)));
    }

    [Fact] public async Task Resubmission_keeps_previous_file_and_exposes_only_current_files_in_the_task()
    {
        await using var h = await Setup(); await ClaimForReview(h); var assigned = await Current(h);
        var first = new TeamCompletionSubmitInput(assigned.Version, Guid.NewGuid(), "Bản đầu", [ReportFile("old.txt", "old")]);
        await Use(h, s => s.SubmitTeamCompletionAsync(Member, TaskId, first, Ct)); var pending = await Current(h); var oldFile = Assert.Single(pending.CompletionFiles!);
        await Use(h, s => s.ReviewTeamCompletionAsync(Leader, TaskId, new(pending.Version, first.RequestId, false, "Bổ sung phần kiểm thử"), Ct));
        var reopened = await Current(h); var second = new TeamCompletionSubmitInput(reopened.Version, Guid.NewGuid(), "Đã bổ sung", [ReportFile("new.txt", "new")]);
        await Use(h, s => s.SubmitTeamCompletionAsync(Member, TaskId, second, Ct));
        Assert.Equal("new.txt", Assert.Single((await Current(h)).CompletionFiles!).Name);
        await Use(h, s => s.SubmitTeamCompletionAsync(Member, TaskId, first, Ct)); // A stale network retry cannot replace the current report.
        Assert.Equal(second.RequestId, (await Current(h)).CompletionRequestId);
        Assert.Equal(2, await h.DbAsync(db => db.TeamCompletionFiles.CountAsync(Ct))); Assert.Equal(2, await h.DbAsync(db => db.TeamCompletionSubmissions.CountAsync(Ct)));
        Assert.Equal("old", Encoding.UTF8.GetString((await Use(h, s => s.DownloadTeamCompletionFileAsync(Leader, TaskId, oldFile.Id, Ct))).Content));
        await h.DbAsync(async db => { (await db.TeamMemberships.SingleAsync(x => x.UserId == Member, Ct)).Active = false; await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DownloadTeamCompletionFileAsync(Member, TaskId, oldFile.Id, Ct)));
    }

    [Fact] public async Task Running_session_blocks_report_and_rolls_back_version_claim_and_file_writes()
    {
        await using var h = await Setup(); await ClaimForReview(h); var assigned = await Current(h);
        await h.DbAsync(async db => { (await db.WorkSessions.SingleAsync(Ct)).Status = WorkSessionStatus.Running; await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.SubmitTeamCompletionAsync(Member, TaskId, new(assigned.Version, Guid.NewGuid(), Files: [ReportFile()]), Ct)));
        Assert.Equal(assigned.Version, (await Current(h)).Version); Assert.Equal("Assigned", (await Current(h)).Status);
        Assert.Equal(0, await h.DbAsync(db => db.TeamCompletionSubmissions.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.TeamCompletionFiles.CountAsync(Ct)));
    }
}
