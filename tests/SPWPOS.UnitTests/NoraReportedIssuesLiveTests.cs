using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class NoraReportedIssuesLiveTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_REPORTED_ISSUES") == "1";
    private static IConfiguration Configuration() {
        var root=Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT")!;
        var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(root,"src/SPWPOS.Api/appsettings.json")).AddUserSecrets("SPWPOS-Authentication-Development").Build();
        Assert.Equal("gemini-3.8-flash",config["AiSettings:Model"]);return config;
    }
    [Fact(SkipUnless=nameof(Enabled),Skip="Opt-in Gemini 3.8 regression on fictional calendar, never a user's saved data.")]
    public async Task Native_flash_moves_the_clock_referenced_work_and_keeps_the_exam_at_1400()
    {
        var ct=TestContext.Current.CancellationToken;using var http=new HttpClient();var model=new TaskInterpreter(http,Configuration(),NullLogger<TaskInterpreter>.Instance);
        await using var h=await ServiceHarness.CreateAsync(s=>{s.AddSingleton<ITaskInterpreter>(model);s.Configure<NoraAgentOptions>(o=>o.Mode="Agent");});
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-02T02:00:00Z")-h.Clock.GetUtcNow());var user=await h.RegisterAsync();
        await using var scope=h.Provider.CreateAsyncScope();var workspace=scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await workspace.SetPreferenceAsync(user.Id,new("Asia/Ho_Chi_Minh",25,240,0,480,1320,MaximumDailyMinutes:480),ct);
        await workspace.SetAvailabilityAsync(user.Id,new(Enumerable.Range(0,7).Select(d=>new AvailabilityInput(d,480,1320)).ToArray()),ct);
        var work=new WorkTask {UserId=user.Id,Name="[TEST] Báo cáo",EstimatedMinutes=80,Deadline=DateTimeOffset.Parse("2026-11-03T22:00:00+07:00")};
        var other=new WorkTask {UserId=user.Id,Name="[TEST] Đọc tài liệu",EstimatedMinutes=60,Deadline=work.Deadline};
        var session=new WorkSession {UserId=user.Id,TaskId=work.Id,Start=DateTimeOffset.Parse("2026-11-03T12:45:00+07:00"),End=DateTimeOffset.Parse("2026-11-03T14:05:00+07:00")};
        var retained=new WorkSession {UserId=user.Id,TaskId=other.Id,Start=DateTimeOffset.Parse("2026-11-03T17:00:00+07:00"),End=DateTimeOffset.Parse("2026-11-03T18:00:00+07:00")};
        await h.DbAsync(async db=>{db.WorkTasks.AddRange(work,other);db.WorkSessions.AddRange(session,retained);await db.SaveChangesAsync(ct);});
        var result=await scope.ServiceProvider.GetRequiredService<IPlanningAgentService>().SendAgentTurnAsync(user.Id,new(Guid.NewGuid(),Guid.NewGuid(),
            "Dời phiên làm việc 12:45–14:05 để kiểm tra lúc 14:00 ngày mai 03/11/2026, bài kiểm tra kết thúc 15:00. Giữ nguyên các việc khác và deadline, cho tôi xem phương án trước.",false),ct);
        Assert.Equal("PREVIEW",result.Status);Assert.NotNull(result.Plan);Assert.Contains(result.Plan.Sessions,s=>s.Id==retained.Id && s.Preserved);
        Assert.Contains(result.Changes,c=>c.Kind=="EVENT" && c.After.Contains("14:00") && c.After.Contains("15:00"));
        Assert.All(result.Plan.Sessions.Where(s=>s.TaskId==work.Id),s=>Assert.True(s.End<=DateTimeOffset.Parse("2026-11-03T14:00:00+07:00") || s.Start>=DateTimeOffset.Parse("2026-11-03T15:00:00+07:00")));
        Assert.Equal(80,result.Plan.Sessions.Where(s=>s.TaskId==work.Id).Sum(s=>(s.End-s.Start).TotalMinutes));
        Assert.Equal(0,await h.DbAsync(db=>db.FixedEvents.CountAsync(ct)));Console.WriteLine($"LIVE move target: PREVIEW, exact exam 14:00–15:00, 80 work minutes preserved, {result.Run!.ModelTurns} model calls.");
    }
    [Fact(SkipUnless=nameof(Enabled),Skip="Opt-in Gemini 3.8 regression on locally generated SSG105 example, no real tasks published.")]
    public async Task Flash_keeps_two_packages_from_SSG105_document_and_accepts_a_confirmed_common_hour()
    {
        var ct=TestContext.Current.CancellationToken;using var http=new HttpClient();var model=new TaskInterpreter(http,Configuration(),NullLogger<TaskInterpreter>.Instance);
        await using var h=await ServiceHarness.CreateAsync(s=>s.AddSingleton<ITaskInterpreter>(model));h.Clock.Advance(DateTimeOffset.Parse("2026-11-02T02:00:00Z")-h.Clock.GetUtcNow());
        var user=await h.RegisterAsync();Guid teamId;
        await using(var scope=h.Provider.CreateAsyncScope())teamId=await scope.ServiceProvider.GetRequiredService<IPrivateTeamService>().CreateTeamAsync(user.Id,new("[TEST] SSG105"),ct);
        await h.DbAsync(async db=>{var member=new ApplicationUser {Id=Guid.NewGuid(),UserName="fake.ssg",NormalizedUserName="FAKE.SSG",Email="fake.ssg@example.com",NormalizedEmail="FAKE.SSG@EXAMPLE.COM",FirstName="Test",IsActive=true,EmailConfirmed=true};db.Users.Add(member);db.TeamMemberships.Add(new(){TeamId=teamId,UserId=member.Id});await db.SaveChangesAsync(ct);});
        var root=Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT")!;
        var file=new TeamBriefFile("SSG105-example.docx","application/vnd.openxmlformats-officedocument.wordprocessingml.document",Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(root,"outputs/SSG105_Ke_hoach_xay_dung_proposal.docx"),ct)));
        await using var request=h.Provider.CreateAsyncScope();var service=request.ServiceProvider.GetRequiredService<IPrivateTeamService>();
        var pending=await service.DraftTeamBriefAsync(user.Id,teamId,new("",file),ct);
        Assert.NotEmpty(pending.Question);Assert.Equal(2,pending.PendingTasks!.Length);Assert.NotEqual(Guid.Empty,pending.ProposalId);
        var ready=await service.DraftTeamBriefAsync(user.Id,teamId,new("Trưởng nhóm xác nhận hạn nộp chung của tất cả gói việc là 21:00 ngày 04/11/2026 theo giờ Việt Nam. Dùng mốc này thay thông tin giờ nộp chưa chốt trong tài liệu.",file),ct);
        Assert.Empty(ready.Question);Assert.Equal(2,ready.Tasks.Length);Assert.All(ready.Tasks,t=>Assert.Equal(DateTimeOffset.Parse("2026-11-04T21:00:00+07:00"),t.Deadline));
        Assert.Equal(0,await h.DbAsync(db=>db.TeamTasks.CountAsync(ct)));Console.WriteLine("LIVE SSG105: 2 retained pending packages, then 2 complete publishable draft packages at leader-confirmed 21:00, no published tasks.");
    }
}
