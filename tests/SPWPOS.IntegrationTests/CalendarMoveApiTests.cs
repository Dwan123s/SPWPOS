using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class CalendarMoveApiTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    [Fact] public async Task Manual_event_API_rejects_past_start_without_saving_the_series()
    {
        await using var f = new ApiFactory(); using var client = await f.ClientAsync();
        await Authorize(f, client);
        var now = f.Clock.GetUtcNow();
        await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Quá khứ", now.AddHours(-1), now.AddHours(1), RepeatWeeks: 3), expected: HttpStatusCode.BadRequest);
        await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Hiện tại", now, now.AddHours(1)), expected: HttpStatusCode.BadRequest);
        var workspace = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("events").EnumerateArray());
        var id = (await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Tương lai", now.AddHours(1), now.AddHours(2)))).GetProperty("data").GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Put, $"/api/time/events/{id}", new EventInput("Dời về quá khứ", now.AddHours(-2), now.AddHours(-1)), expected: HttpStatusCode.BadRequest);
        workspace = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Equal(now.AddHours(1), Assert.Single(workspace.GetProperty("events").EnumerateArray()).GetProperty("start").GetDateTimeOffset());
    }
    [Theory]
    [InlineData("confirm")]
    [InlineData("locked")]
    [InlineData("stale")]
    [InlineData("blocked")]
    public async Task Meeting_preview_handles_flexible_protected_and_stale_sessions(string scenario)
    {
        await using var f = new ApiFactory();
        using var client = await f.ClientAsync();
        await Authorize(f, client);
        f.Clock.Advance(DateTimeOffset.Parse("2026-11-11T09:00:00Z") - f.Clock.GetUtcNow());
        var now = f.Clock.GetUtcNow();
        Guid movedId, keptId;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uid = await db.Users.Select(u => u.Id).SingleAsync(Ct);
            db.WorkPreferences.Add(new() { UserId = uid, TimeZoneId = "UTC", BreakMinutes = 0,
                MinimumSessionMinutes = 25, MaximumSessionMinutes = 120, MaximumDailyMinutes = 1440 });
            db.WeeklyAvailability.AddRange(Enumerable.Range(0, 7).Select(d => new WeeklyAvailability
                { UserId = uid, DayOfWeek = d, StartMinute = 0, EndMinute = 1440 }));
            if (scenario == "blocked")
            {
                foreach (var row in db.ChangeTracker.Entries<WeeklyAvailability>().ToArray()) row.State = EntityState.Detached;
                db.WeeklyAvailability.Add(new() { UserId = uid, DayOfWeek = (int)now.DayOfWeek, StartMinute = 600, EndMinute = 840 });
                db.WeeklyAvailability.Add(new() { UserId = uid, DayOfWeek = (int)now.DayOfWeek, StartMinute = 900, EndMinute = 1020 });
            }
            var task = new WorkTask { UserId = uid, Name = "Five-hour report", EstimatedMinutes = 300,
                Deadline = now.AddDays(1), IsHardDeadline = true, Splittable = true, MinimumSessionMinutes = 25 };
            var moved = new WorkSession { UserId = uid, TaskId = task.Id, Start = now.AddHours(1), End = now.AddHours(2),
                IsLocked = scenario == "locked" };
            var kept = new WorkSession { UserId = uid, TaskId = task.Id, Start = now.AddHours(3), End = now.AddHours(5) };
            movedId = moved.Id;
            keptId = kept.Id;
            var later = new WorkSession { UserId = uid, TaskId = task.Id, Start = now.AddHours(6), End = now.AddHours(8) };
            if (scenario == "blocked")
                db.FixedEvents.Add(new() { UserId = uid, Name = "Protected event", Start = now.AddHours(2), End = now.AddHours(3) });
            db.AddRange(task, moved, kept, later);
            await db.SaveChangesAsync(Ct);
        }

        var meeting = new EventInput("Meeting", now.AddHours(1), now.AddHours(2));
        await Send(client, HttpMethod.Post, "/api/time/events", meeting, expected: HttpStatusCode.Conflict);
        if (scenario == "locked")
        {
            await Send(client, HttpMethod.Post, "/api/time/events/replan/preview", meeting, expected: HttpStatusCode.Conflict);
            Assert.Empty((await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data").GetProperty("events").EnumerateArray());
            return;
        }
        var preview = (await Send(client, HttpMethod.Post, "/api/time/events/replan/preview", meeting)).GetProperty("data");
        if (scenario == "blocked")
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Null, preview.GetProperty("id").ValueKind);
            var blocked = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
            Assert.Equal("Protected event", Assert.Single(blocked.GetProperty("events").EnumerateArray()).GetProperty("name").GetString());
            return;
        }
        var proposalId = preview.GetProperty("id").GetGuid();
        Assert.Equal(movedId, Assert.Single(preview.GetProperty("before").EnumerateArray()).GetProperty("id").GetGuid());
        var before = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(before.GetProperty("events").EnumerateArray());
        var path = $"/api/time/events/replan/{proposalId}/confirm";
        if (scenario == "stale")
        {
            await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Unrelated", now.AddHours(9), now.AddHours(10)));
            await Send(client, HttpMethod.Post, path, expected: HttpStatusCode.Conflict);
            var stale = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
            Assert.Equal("Unrelated", Assert.Single(stale.GetProperty("events").EnumerateArray()).GetProperty("name").GetString());
            Assert.Contains(stale.GetProperty("sessions").EnumerateArray(), s => s.GetProperty("id").GetGuid() == movedId);
            return;
        }
        var confirmed = (await Send(client, HttpMethod.Post, path)).GetProperty("data");
        var replay = (await Send(client, HttpMethod.Post, path)).GetProperty("data");
        Assert.Equal(confirmed.GetProperty("id").GetGuid(), replay.GetProperty("id").GetGuid());

        await using var verify = f.Services.CreateAsyncScope();
        var saved = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(meeting.Start, (await saved.FixedEvents.SingleAsync(Ct)).Start);
        var sessions = await saved.WorkSessions.Where(s => s.Status == WorkSessionStatus.Scheduled).ToArrayAsync(Ct);
        var keptAfter = Assert.Single(sessions, s => s.Id == keptId);
        Assert.Equal(now.AddHours(3), keptAfter.Start);
        Assert.DoesNotContain(sessions, s => s.Start < meeting.End && meeting.Start < s.End);
        Assert.Equal(300, sessions.Sum(s => (s.End - s.Start).TotalMinutes));
    }

    [Fact]public async Task Drag_drop_HTTP_preserves_type_checks_revision_and_supports_undo(){
        await using var f=new ApiFactory();using var client=await f.ClientAsync();
        using(var anon=await client.PostAsync("/api/calendar/move",new StringContent("{}",System.Text.Encoding.UTF8,"application/json"),Ct))Assert.Equal(HttpStatusCode.Unauthorized,anon.StatusCode);
        await Authorize(f,client);await SetupTime(client);
        await Send(client,HttpMethod.Put,"/api/time/preferences",new PreferenceInput("UTC",25,120,0,480,1320));
        var start=f.Clock.GetUtcNow().AddDays(2);start=new(start.Date.AddHours(10),TimeSpan.Zero);
        var eventId=(await Send(client,HttpMethod.Post,"/api/time/events",new EventInput("Cuộc hẹn",start,start.AddHours(1),"Nội dung",EventType:"APPOINTMENT",IsUrgent:true))).GetProperty("data").GetProperty("id").GetGuid();
        var workspace=(await Send(client,HttpMethod.Get,"/api/workspace")).GetProperty("data");var revision=workspace.GetProperty("preference").GetProperty("revision").GetGuid();
        var move=new CalendarMoveInput("Event",eventId,start.AddHours(2),start.AddHours(3),revision,start,start.AddHours(1));
        var response=(await Send(client,HttpMethod.Post,"/api/calendar/move",move)).GetProperty("data");
        var newRevision=response.GetProperty("revision").GetGuid();Assert.NotEqual(revision,newRevision);
        await Send(client,HttpMethod.Post,"/api/calendar/move",move,expected:HttpStatusCode.Conflict);
        await Send(client,HttpMethod.Post,"/api/calendar/move",new CalendarMoveInput("Event",eventId,start,start.AddHours(1),newRevision,move.Start,move.End));
        var after=(await Send(client,HttpMethod.Get,"/api/workspace")).GetProperty("data");var entry=Assert.Single(after.GetProperty("events").EnumerateArray());
        Assert.Equal(start,entry.GetProperty("start").GetDateTimeOffset());Assert.Equal("Nội dung",entry.GetProperty("description").GetString());Assert.True(entry.GetProperty("isUrgent").GetBoolean());Assert.Empty(after.GetProperty("tasks").EnumerateArray());
        await Send(client,HttpMethod.Post,"/api/calendar/move",move with{Kind="Unknown"},expected:HttpStatusCode.BadRequest);
    }
}
