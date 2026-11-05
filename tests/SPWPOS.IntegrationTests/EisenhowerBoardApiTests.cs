using System.Net;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;
namespace SPWPOS.IntegrationTests;

public sealed class EisenhowerBoardApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact] public async Task Quick_capture_HTTP_accepts_no_deadline_or_duration_and_roundtrips_through_Inbox()
    {
        await using var f = new ApiFactory(); using var client = await f.ClientAsync();
        await Authorize(f, client);
        var input = new MatrixDraftInput("Nhớ một ý tưởng", "Mô tả ngắn", Quadrant: "Q2", RequestId: Guid.NewGuid());
        var id = (await Send(client, HttpMethod.Post, "/api/eisenhower/drafts", input)).GetProperty("data").GetProperty("id").GetGuid();
        var repeated = (await Send(client, HttpMethod.Post, "/api/eisenhower/drafts", input)).GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal(id, repeated);
        var view = (await Send(client, HttpMethod.Get, "/api/eisenhower")).GetProperty("data");
        var row = Assert.Single(view.GetProperty("tasks").EnumerateArray());
        Assert.Equal("Draft", row.GetProperty("kind").GetString()); Assert.Equal("Q2", row.GetProperty("quadrant").GetString());
        Assert.True(!row.TryGetProperty("deadline", out var due) || due.ValueKind == System.Text.Json.JsonValueKind.Null);
        var notes = (await Send(client, HttpMethod.Get, "/api/notes")).GetProperty("data");
        Assert.Equal(id, Assert.Single(notes.EnumerateArray()).GetProperty("id").GetGuid());
        var workspace = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("tasks").EnumerateArray()); Assert.Empty(workspace.GetProperty("sessions").EnumerateArray());
        var deadline = f.Clock.GetUtcNow().AddDays(4);
        await Send(client, HttpMethod.Put, $"/api/eisenhower/drafts/{id}", input with { Deadline = deadline, Version = row.GetProperty("version").GetGuid() });
        view = (await Send(client, HttpMethod.Get, "/api/eisenhower")).GetProperty("data");
        Assert.Equal(deadline, Assert.Single(view.GetProperty("tasks").EnumerateArray()).GetProperty("deadline").GetDateTimeOffset());
    }
    public static bool SqlEnabled => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPWPOS_TEST_SQLSERVER"));
    [Fact] public Task Matrix_HTTP_create_checklist_move_and_calendar_preview_are_guarded_and_persisted() => Verify(false);
    [Fact(SkipUnless=nameof(SqlEnabled),Skip="Opt-in SQL Server migration and persistence check.")]
    public Task Matrix_SQL_Server_migrations_and_roundtrip_preserve_calendar_boundaries() => Verify(true);
    private async Task Verify(bool sqlServer)
    {
        await using var f = new ApiFactory(sqlServer); using var client = await f.ClientAsync();
        using (var anon = await client.GetAsync("/api/eisenhower",Ct)) Assert.Equal(HttpStatusCode.Unauthorized,anon.StatusCode);
        await Authorize(f,client); await SetupTime(client);
        var deadline = f.Clock.GetUtcNow().AddDays(5);
        var checklist = new TaskChecklistItem[] { new(Guid.NewGuid(),"Đọc đề"),new(Guid.NewGuid(),"Viết nháp") };
        var id = (await Send(client,HttpMethod.Post,"/api/eisenhower/tasks",new MatrixTaskInput("Report","Đầu ra cần nộp",deadline,60,"Q2",Checklist:checklist)))
            .GetProperty("data").GetProperty("id").GetGuid();
        var view = (await Send(client,HttpMethod.Get,"/api/eisenhower")).GetProperty("data");
        var row = Assert.Single(view.GetProperty("tasks").EnumerateArray()); var version = row.GetProperty("version").GetGuid();
        Assert.Equal("Q2",row.GetProperty("quadrant").GetString());
        var done = checklist.Reverse().Select(i=>i with {Completed=true}).ToArray();
        await Send(client,HttpMethod.Put,$"/api/eisenhower/tasks/{id}/checklist",new MatrixChecklistInput(version,done));
        await Send(client,HttpMethod.Put,$"/api/eisenhower/tasks/{id}/checklist",new MatrixChecklistInput(version,[]),expected:HttpStatusCode.Conflict);
        view = (await Send(client,HttpMethod.Get,"/api/eisenhower")).GetProperty("data"); row = view.GetProperty("tasks")[0];
        var move = new MatrixMoveInput(row.GetProperty("version").GetGuid(),view.GetProperty("revision").GetGuid(),"Q4");
        await Send(client,HttpMethod.Post,$"/api/eisenhower/tasks/{id}/position",move);
        await Send(client,HttpMethod.Post,$"/api/eisenhower/tasks/{id}/position",move,expected:HttpStatusCode.Conflict);
        view = (await Send(client,HttpMethod.Get,"/api/eisenhower")).GetProperty("data"); row = view.GetProperty("tasks")[0];
        Assert.Equal("Q4",row.GetProperty("quadrant").GetString()); Assert.Equal(deadline,row.GetProperty("deadline").GetDateTimeOffset());
        Assert.Equal(checklist[1].Id,row.GetProperty("checklist")[0].GetProperty("id").GetGuid());
        var schedule = new MatrixScheduleInput(row.GetProperty("version").GetGuid(),view.GetProperty("revision").GetGuid());
        await Send(client,HttpMethod.Post,$"/api/eisenhower/tasks/{id}/schedule",schedule,expected:HttpStatusCode.Conflict);
        var plan = (await Send(client,HttpMethod.Post,$"/api/eisenhower/tasks/{id}/schedule",schedule with {ExplicitRequest=true})).GetProperty("data");
        Assert.NotEmpty(plan.GetProperty("sessions").EnumerateArray()); Assert.False(plan.GetProperty("isApplied").GetBoolean());
        var workspace = (await Send(client,HttpMethod.Get,"/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("sessions").EnumerateArray());
        Assert.Equal(0,Assert.Single(workspace.GetProperty("tasks").EnumerateArray()).GetProperty("progress").GetInt32());
        await Send(client,HttpMethod.Post,$"/api/plans/{plan.GetProperty("id").GetGuid()}/apply");
        workspace = (await Send(client,HttpMethod.Get,"/api/workspace")).GetProperty("data");
        Assert.NotEmpty(workspace.GetProperty("sessions").EnumerateArray());
        view = (await Send(client,HttpMethod.Get,"/api/eisenhower")).GetProperty("data"); row = view.GetProperty("tasks")[0];
        await Send(client,HttpMethod.Post,$"/api/eisenhower/tasks/{id}/position",new MatrixMoveInput(row.GetProperty("version").GetGuid(),view.GetProperty("revision").GetGuid(),"Q9"),expected:HttpStatusCode.BadRequest);
    }
}
