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
