using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed partial class PrivateTeamPlanningTests
{
    private static TeamBriefAnalysis CompleteBrief(TeamBriefContext context) => new("Đồ án quản lý thời gian", "", "Chia thành các gói việc theo số người đang hoạt động.",
        Enumerable.Range(1, context.MemberCount).Select(i => new TeamBriefTask("Gói việc " + i, "Tổng hợp yêu cầu hạng mục " + i,
            ["Đọc yêu cầu trong mục " + i], ["Báo cáo và mã nguồn hạng mục " + i], ["Đáp ứng đủ yêu cầu; chạy kiểm thử"],
            ["Phối hợp bàn giao với các gói việc khác"], ["Mục " + i], 120, Tomorrow, "hạn ngày mai")).ToArray(), []);
    private static TeamBriefFile TextBrief(string text, string name = "Yêu cầu.txt") => new(name, "text/plain", Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
    private static TeamBriefInput BriefInput => new("Bổ sung: hạn ngày mai", TextBrief("Đồ án cần bản thiết kế, mã nguồn và báo cáo."));

    [Fact] public async Task Brief_reads_source_and_member_count_without_private_context_then_publishes_all_tasks_once()
    {
        var ai = new AgentAiTestDouble { BriefResponse = CompleteBrief }; await using var h = await Setup(ai);
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct));
        Assert.NotEqual(Guid.Empty, draft.ProposalId); Assert.Equal(2, draft.MemberCount); Assert.Equal(2, draft.Tasks.Length);
        Assert.Contains("bản thiết kế", ai.BriefContext!.DocumentText); Assert.Equal(2, ai.BriefContext.MemberCount);
        Assert.DoesNotContain("PRIVATE_CLASS", JsonSerializer.Serialize(ai.BriefContext)); Assert.DoesNotContain("@example", JsonSerializer.Serialize(ai.BriefContext));
        Assert.Equal(1, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct))); Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct)));
        var result = await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct));
        var retry = await Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct)); Assert.Equal(result.TaskIds, retry.TaskIds);
        var created = (await Use(h, s => s.GetTeamAsync(Member, Team, Ct))).Tasks.Where(x => result.TaskIds.Contains(x.Id)).ToArray();
        Assert.Equal(2, created.Length); Assert.All(created, task => {
            Assert.Null(task.AssigneeId); Assert.Equal("Open", task.Status); Assert.Equal(Tomorrow, task.Deadline);
            foreach (var label in new[] { "TÓM TẮT", "YÊU CẦU CẦN THỰC HIỆN", "ĐẦU RA CẦN BÀN GIAO", "TIÊU CHÍ HOÀN THÀNH", "GHI CHÚ & PHỐI HỢP", "THAM CHIẾU TÀI LIỆU", "CĂN CỨ DEADLINE", "Yêu cầu.txt" }) Assert.Contains(label, task.Description);
        });
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(Ct))); Assert.Equal(2, await h.DbAsync(db => db.TeamActivities.CountAsync(x => x.Kind == "TASK_CREATED", Ct)));
        var claim = await Use(h, s => s.PreviewTeamClaimAsync(Member, result.TaskIds[0], Ct));
        await Use(h, s => s.ConfirmTeamClaimAsync(Member, claim.Id, new(), Ct));
        Assert.Contains("TIÊU CHÍ HOÀN THÀNH", (await h.DbAsync(db => db.WorkTasks.SingleAsync(Ct))).Description);
    }

    [Fact] public async Task Only_current_leader_and_owner_can_analyze_and_publish_brief()
    {
        var ai = new AgentAiTestDouble { BriefResponse = CompleteBrief }; await using var h = await Setup(ai);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DraftTeamBriefAsync(Member, Team, BriefInput, Ct))); Assert.Equal(0, ai.Calls);
        var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct));
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamBriefAsync(Member, Team, draft.ProposalId, Ct)));
        await h.DbAsync(async db => { (await db.TeamMemberships.SingleAsync(x => x.UserId == Leader, Ct)).Role = "Member"; await db.SaveChangesAsync(Ct); });
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct)));
        Assert.Equal(1, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct)));
    }

    [Theory] [InlineData("beforeConfirm")] [InlineData("duringAI")] [InlineData("expired")]
    public async Task Brief_requires_fresh_membership_and_live_draft(string change)
    {
        var ai = new AgentAiTestDouble { BriefResponse = CompleteBrief }; await using var h = await Setup(ai);
        async Task AddMember() => await h.DbAsync(async db => { db.TeamMemberships.Add(new() { TeamId = Team, UserId = Other }); await db.SaveChangesAsync(Ct); });
        if (change == "duringAI") ai.BeforeReply = AddMember;
        if (change == "duringAI") await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct)));
        else
        {
            var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct));
            if (change == "beforeConfirm") await AddMember(); else h.Clock.Advance(TimeSpan.FromMinutes(31));
            await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.ConfirmTeamBriefAsync(Leader, Team, draft.ProposalId, Ct)));
        }
        Assert.Equal(1, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct)));
    }

    [Theory] [InlineData("missingDeadline")] [InlineData("inventedEvidence")] [InlineData("question")] [InlineData("wrongCount")]
    public async Task Incomplete_or_unsupported_deadlines_never_create_publishable_tasks(string kind)
    {
        var ai = new AgentAiTestDouble { BriefResponse = c => {
            var result = CompleteBrief(c);
            return kind switch {
                "missingDeadline" => result with { Tasks = result.Tasks.Select(x => x with { Deadline = null }).ToArray() },
                "inventedEvidence" => result with { Tasks = result.Tasks.Select(x => x with { DeadlineEvidence = "deadline không có trong nguồn" }).ToArray() },
                "question" => result with { Question = "Hạn nào là hạn cuối?" },
                _ => result with { Tasks = result.Tasks.Take(1).ToArray() }
            };
        } }; await using var h = await Setup(ai);
        if (kind == "wrongCount") await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct)));
        else { var draft = await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, BriefInput, Ct)); Assert.NotEqual(Guid.Empty, draft.ProposalId); Assert.NotEmpty(draft.Question); Assert.Empty(draft.Tasks); Assert.Equal(2, draft.PendingTasks!.Length);
            await Assert.ThrowsAsync<AuthException>(()=>Use(h,s=>s.ConfirmTeamBriefAsync(Leader,Team,draft.ProposalId,Ct))); }
        Assert.Equal(kind=="wrongCount" ? 0 : 1, await h.DbAsync(db => db.PrivatePlanningProposals.CountAsync(Ct))); Assert.Equal(1, await h.DbAsync(db => db.TeamTasks.CountAsync(Ct)));
    }

    [Fact] public async Task Word_tables_and_paragraphs_are_read_and_pdf_is_passed_directly_to_the_model()
    {
        var ai = new AgentAiTestDouble { BriefResponse = CompleteBrief }; await using var h = await Setup(ai);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("word/document.xml"); using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Yêu cầu dự án</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>hạn ngày mai</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:body></w:document>");
        }
        await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, new("", new("brief.docx", "application/octet-stream", Convert.ToBase64String(stream.ToArray()))), Ct));
        Assert.Contains("Yêu cầu dự án", ai.BriefContext!.DocumentText); Assert.Contains("hạn ngày mai", ai.BriefContext.DocumentText);
        var pdf = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.7 fake document for transport test"));
        await Use(h, s => s.DraftTeamBriefAsync(Leader, Team, new("hạn ngày mai", new("brief.pdf", "application/pdf", pdf)), Ct));
        Assert.Null(ai.BriefContext!.DocumentText); Assert.Equal(pdf, ai.BriefContext.PdfData);
    }

    [Theory] [InlineData("brief.exe", "hello")] [InlineData("brief.pdf", "not a PDF")] [InlineData("brief.docx", "not a zip")]
    public async Task Unsupported_or_corrupt_files_fail_before_any_AI_call(string name, string content)
    {
        var ai = new AgentAiTestDouble { BriefResponse = CompleteBrief }; await using var h = await Setup(ai);
        await Assert.ThrowsAsync<AuthException>(() => Use(h, s => s.DraftTeamBriefAsync(Leader, Team, new("hạn ngày mai", TextBrief(content, name)), Ct)));
        Assert.Equal(0, ai.Calls); Assert.Equal(0, await h.DbAsync(db => db.PrivatePlanningProposals.CountAsync(Ct)));
    }
}
