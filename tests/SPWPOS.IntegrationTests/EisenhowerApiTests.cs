using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class EisenhowerApiTests(Xunit.ITestOutputHelper output)
{
    [Fact] public async Task Eisenhower_reads_are_private_and_metadata_changes_require_owner_or_leader_with_current_version()
    {
        var ct=TestContext.Current.CancellationToken; await using var f=new ApiFactory(); using var leader=await f.ClientAsync();
        var tokens=await AuthFlowTests.RegisterLoginAsync(f,leader); leader.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens.Access);
        var memberId=Guid.NewGuid(); var teamId=Guid.NewGuid(); var sharedId=Guid.NewGuid(); var taskId=Guid.NewGuid(); Guid version;
        await using(var scope=f.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var leaderId=await db.Users.Select(u=>u.Id).SingleAsync(ct);
            var member=new ApplicationUser {Id=memberId,UserName="eisenhower.member",Email="eisenhower@example.com",FirstName="Member",EmailConfirmed=true,IsActive=true};
            var created=await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(member,ServiceHarness.Password); Assert.True(created.Succeeded);
            db.TeamProjects.Add(new(){Id=teamId,CreatorId=leaderId,Name="Eisenhower group"});
            db.TeamMemberships.AddRange(new TeamMembership{TeamId=teamId,UserId=leaderId,Role="Leader"},new TeamMembership{TeamId=teamId,UserId=memberId});
            db.TeamTasks.Add(new(){Id=sharedId,TeamId=teamId,Name="Shared",AssigneeId=memberId,Status="Assigned",ImportanceLevel="Important",Deadline=f.Clock.GetUtcNow().AddDays(2),EstimatedMinutes=60});
            var personal=new WorkTask{Id=taskId,UserId=memberId,Name="PRIVATE_MEMBER_TASK",Deadline=f.Clock.GetUtcNow().AddDays(2),EstimatedMinutes=60}; version=personal.Version;
            db.WorkTasks.Add(personal); db.WorkPreferences.Add(new(){UserId=memberId,TimeZoneId="UTC"});
            db.FixedEvents.Add(new(){UserId=memberId,Name="PRIVATE_MEMBER_CLASS",Start=f.Clock.GetUtcNow().AddHours(1),End=f.Clock.GetUtcNow().AddHours(2)});
            db.WeeklyAvailability.AddRange(Enumerable.Range(0,7).Select(d=>new WeeklyAvailability{UserId=memberId,DayOfWeek=d,StartMinute=0,EndMinute=1440}));
            db.TeamTaskAssignments.Add(new(){TeamTaskId=sharedId,UserId=memberId,WorkTaskId=taskId,ProposalId=Guid.NewGuid()}); await db.SaveChangesAsync(ct);
        }
        using var memberClient=f.CreateClient(); memberClient.BaseAddress=new Uri("https://localhost");
        var login=await memberClient.PostAsJsonAsync("/api/auth/login",new{email="eisenhower@example.com",password=ServiceHarness.Password},ct); Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        using var session=JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)); memberClient.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.RootElement.GetProperty("data").GetProperty("accessToken").GetString());
        var view=await memberClient.GetStringAsync("/api/eisenhower",ct); Assert.Contains("PRIVATE_MEMBER_TASK",view); Assert.DoesNotContain("PRIVATE_MEMBER_CLASS",view);
        using var personalView=JsonDocument.Parse(view); var item=Assert.Single(personalView.RootElement.GetProperty("data").GetProperty("tasks").EnumerateArray());
        Assert.Equal("Important",item.GetProperty("importance").GetString()); Assert.Equal("Accepted",item.GetProperty("kind").GetString());
        var leaderView=await leader.GetStringAsync($"/api/eisenhower?userId={memberId}",ct); Assert.DoesNotContain("PRIVATE_MEMBER_TASK",leaderView);
        Assert.Equal(HttpStatusCode.NotFound,(await leader.GetAsync($"/api/tasks/{taskId}/eisenhower",ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await leader.GetAsync($"/api/teams/tasks/{sharedId}/eisenhower",ct)).StatusCode);
        var update=new ImportanceInput(version,"Lower","Công việc hỗ trợ cá nhân");
        Assert.Equal(HttpStatusCode.OK,(await memberClient.PostAsJsonAsync($"/api/tasks/{taskId}/importance",update,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await memberClient.PostAsJsonAsync($"/api/tasks/{taskId}/importance",update,ct)).StatusCode);
        Guid sharedVersion;
        await using(var scope=f.Services.CreateAsyncScope()) sharedVersion=await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TeamTasks.Where(t=>t.Id==sharedId).Select(t=>t.Version).SingleAsync(ct);
        Assert.Equal(HttpStatusCode.Forbidden,(await memberClient.PostAsJsonAsync($"/api/teams/tasks/{sharedId}/importance",new ImportanceInput(sharedVersion,"Lower","Không được sửa chung"),ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await leader.PostAsJsonAsync($"/api/teams/tasks/{sharedId}/importance",new ImportanceInput(sharedVersion,"Important","Đầu ra bắt buộc"),ct)).StatusCode);
        await using(var verify=f.Services.CreateAsyncScope()) {
            var db=verify.ServiceProvider.GetRequiredService<ApplicationDbContext>(); Assert.Equal(0,await db.WorkSessions.CountAsync(ct));
            Assert.Equal(0,await db.PriorityAlertStates.CountAsync(ct)); Assert.Equal(0,await db.InAppNotifications.CountAsync(n=>n.Kind=="EISENHOWER_ALERT",ct));
        }
        memberClient.DefaultRequestHeaders.Authorization=null; Assert.Equal(HttpStatusCode.Unauthorized,(await memberClient.GetAsync("/api/eisenhower",ct)).StatusCode);
    }
    [Fact] public async Task Cached_assessment_for_100_tasks_returns_bounded_metadata_without_AI_or_business_writes()
    {
        var ct=TestContext.Current.CancellationToken; await using var f=new ApiFactory(); using var client=await f.ClientAsync();
        var tokens=await AuthFlowTests.RegisterLoginAsync(f,client); client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens.Access);
        await using(var scope=f.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var uid=await db.Users.Select(u=>u.Id).SingleAsync(ct);
            db.WorkPreferences.Add(new(){UserId=uid,TimeZoneId="UTC"}); db.FixedEvents.Add(new(){UserId=uid,Name="Class",Start=f.Clock.GetUtcNow().AddHours(1),End=f.Clock.GetUtcNow().AddHours(2)});
            db.WeeklyAvailability.AddRange(Enumerable.Range(0,7).Select(d=>new WeeklyAvailability{UserId=uid,DayOfWeek=d,StartMinute=480,EndMinute=1320}));
            db.WorkTasks.AddRange(Enumerable.Range(0,100).Select(i=>new WorkTask{UserId=uid,Name="Fixture "+i,Deadline=f.Clock.GetUtcNow().AddDays(1+i%7),EstimatedMinutes=25,ImportanceLevel=i%2==0?"Important":"Lower"}));
            await db.SaveChangesAsync(ct);
        }
        var timer=Stopwatch.StartNew(); var first=await client.GetAsync("/api/eisenhower",ct); timer.Stop(); Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var cold=timer.Elapsed.TotalMilliseconds; var times=new List<double>();
        for(var i=0;i<10;i++){timer.Restart(); var next=await client.GetAsync("/api/eisenhower",ct); timer.Stop(); Assert.Equal(HttpStatusCode.OK,next.StatusCode); times.Add(timer.Elapsed.TotalMilliseconds);}
        times.Sort(); output.WriteLine($"Eisenhower 100 tasks / 7 days: cold={cold:F1}ms; warm p95={times[9]:F1}ms.");
        using var data=JsonDocument.Parse(await first.Content.ReadAsStringAsync(ct)); Assert.Equal(100,data.RootElement.GetProperty("data").GetProperty("tasks").GetArrayLength());
        await using var verify=f.Services.CreateAsyncScope(); var final=verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0,await final.WorkSessions.CountAsync(ct)); Assert.Equal(0,await final.AgentTurns.CountAsync(ct)); Assert.Equal(0,await final.PriorityAlertStates.CountAsync(ct));
    }
}
