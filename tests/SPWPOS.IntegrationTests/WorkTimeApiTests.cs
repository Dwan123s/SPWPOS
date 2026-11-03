using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;
public sealed class WorkTimeApiTests
{
    [Fact] public async Task End_of_session_workflow_requires_confirmation_then_releases_future_work_on_completion() {
        var ct=TestContext.Current.CancellationToken;await using var f=new ApiFactory();using var client=await f.ClientAsync();
        var tokens=await AuthFlowTests.RegisterLoginAsync(f,client);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens.Access);
        WorkTask task;WorkSession source;
        await using(var scope=f.Services.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var uid=await db.Users.Select(u=>u.Id).SingleAsync(ct);var now=f.Clock.GetUtcNow();
            db.WorkPreferences.Add(new(){UserId=uid,TimeZoneId="UTC",BreakMinutes=0,MinimumSessionMinutes=25,MaximumSessionMinutes=120,MaximumDailyMinutes=1440});
            db.WeeklyAvailability.AddRange(Enumerable.Range(0,7).Select(d=>new WeeklyAvailability{UserId=uid,DayOfWeek=d,StartMinute=0,EndMinute=1440}));
            task=new(){UserId=uid,Name="API report",EstimatedMinutes=120,Deadline=now.AddDays(1),IsHardDeadline=true};
            source=new(){UserId=uid,TaskId=task.Id,Start=now.AddMinutes(-121),End=now.AddMinutes(-1),Status=WorkSessionStatus.NeedsReview};
            db.WorkTasks.Add(task);db.WorkSessions.Add(source);await db.SaveChangesAsync(ct);
        }
        var reviews=await client.GetAsync("/api/sessions/time-review",ct);reviews.EnsureSuccessStatusCode();Assert.Contains(source.Id.ToString(),await reviews.Content.ReadAsStringAsync(ct));
        var response=await client.PostAsJsonAsync($"/api/sessions/{source.Id}/extra-time/preview",new ExtraTimeInput(30),ct);response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var preview=doc.RootElement.GetProperty("data");Assert.True(preview.GetProperty("feasible").GetBoolean());
        var id=preview.GetProperty("id").GetGuid();var revision=preview.GetProperty("revision").GetGuid();
        await using(var scope=f.Services.CreateAsyncScope())Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WorkSessions.CountAsync(ct));
        var endpoint=$"/api/sessions/extra-time/{id}/confirm";
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(endpoint,new ExtraTimeConfirm(revision),ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(endpoint,new ExtraTimeConfirm(revision),ct)).StatusCode);
        await using(var scope=f.Services.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();Assert.Equal(2,await db.WorkSessions.CountAsync(ct));
            var unchanged=await db.WorkTasks.SingleAsync(ct);Assert.Equal(120,unchanged.EstimatedMinutes);Assert.Equal(task.Deadline,unchanged.Deadline);Assert.NotEqual(WorkTaskStatus.Completed,unchanged.Status);
        }
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/sessions/{source.Id}/finish-task",null,ct)).StatusCode);
        await using(var scope=f.Services.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();Assert.Equal(WorkTaskStatus.Completed,(await db.WorkTasks.SingleAsync(ct)).Status);
            Assert.Equal(0,await db.WorkSessions.CountAsync(s=>s.Status==WorkSessionStatus.Scheduled,ct));
        }
        client.DefaultRequestHeaders.Authorization=null;Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/sessions/time-review",ct)).StatusCode);
    }
}
