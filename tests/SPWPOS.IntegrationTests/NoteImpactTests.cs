using System.Net;
using SPWPOS.Application.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.IntegrationTests;

public sealed class NoteImpactTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] public async Task Selected_tasks_are_checked_together_and_overload_is_not_persisted()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory, client);
        var start = new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 0, 480, 600));
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/availability", new WeeklyInput([new((int)start.DayOfWeek, 480, 600)]));
        var input = new ScenarioInput(new(start, 1, IsReschedule: true),
            new("Báo cáo", "", start.AddHours(2), 60, IsHardDeadline: true),
            [new("Rà soát", "", start.AddHours(2), 60, IsHardDeadline: true)]);
        var result = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans/scenario", input)).GetProperty("data");
        Assert.Equal(120, result.GetProperty("addedMinutes").GetInt32());
        Assert.Equal(120, result.GetProperty("after").GetProperty("horizonDemandMinutes").GetInt32());
        Assert.Equal("Overload", result.GetProperty("after").GetProperty("riskLevel").GetString());
        Assert.Equal(2, result.GetProperty("after").GetProperty("tasks").GetArrayLength());
        Assert.Contains(result.GetProperty("proposedPlan").GetProperty("warnings").EnumerateArray(), warning => warning.GetProperty("unscheduledMinutes").GetInt32() > 0);
        Assert.Equal(Guid.Empty, result.GetProperty("proposedPlan").GetProperty("id").GetGuid());
        var workspace = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Empty(workspace.GetProperty("tasks").EnumerateArray());
        Assert.Empty(workspace.GetProperty("sessions").EnumerateArray());
    }

    [Fact] public async Task Note_impact_preserves_existing_sessions_and_checks_only_new_work_for_scheduling()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory, client); await WorkspaceFlowTests.SetupTime(client);
        var now = factory.Clock.GetUtcNow();
        var start = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        await WorkspaceFlowTests.Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 90, 0, 480, 1080));
        var taskId = await WorkspaceFlowTests.Create(client, start, "Đang có");
        var plan = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans", new PlanInput(start, 1))).GetProperty("data");
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply");
        var original = plan.GetProperty("sessions")[0];
        var before = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        var input = new ScenarioInput(new(start, 1, IsReschedule: true), new("Việc mới A", "", start.AddDays(1), 40), [new("Việc mới B", "", start.AddDays(1), 40)]);
        var result = (await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans/scenario", input)).GetProperty("data");
        Assert.Equal(80, result.GetProperty("addedMinutes").GetInt32());
        var preserved = Assert.Single(result.GetProperty("proposedPlan").GetProperty("sessions").EnumerateArray(), session => session.GetProperty("taskId").GetGuid() == taskId);
        Assert.True(preserved.GetProperty("preserved").GetBoolean());
        Assert.Equal(original.GetProperty("id").GetGuid(), preserved.GetProperty("id").GetGuid());
        Assert.Equal(original.GetProperty("start").GetDateTimeOffset(), preserved.GetProperty("start").GetDateTimeOffset());
        var after = (await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        Assert.Single(after.GetProperty("tasks").EnumerateArray());
        Assert.Single(after.GetProperty("sessions").EnumerateArray());
        Assert.Equal(before.GetProperty("preference").GetProperty("revision").GetGuid(), after.GetProperty("preference").GetProperty("revision").GetGuid());
    }

    [Theory]
    [InlineData("duration")]
    [InlineData("category")]
    [InlineData("parent")]
    [InlineData("limit")]
    public async Task Every_additional_task_is_validated_before_simulation(string invalid)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        await WorkspaceFlowTests.Authorize(factory, client);
        var now = factory.Clock.GetUtcNow();
        var good = new TaskInput("Hợp lệ", "", now.AddDays(1), 60);
        var other = invalid switch
        {
            "duration" => good with { EstimatedMinutes = 0 },
            "category" => good with { CategoryId = Guid.NewGuid() },
            "parent" => good with { ParentId = Guid.NewGuid() },
            _ => good
        };
        var input = new ScenarioInput(new(now, 1), good, invalid == "limit" ? Enumerable.Repeat(good, 12).ToArray() : [other]);
        await WorkspaceFlowTests.Send(client, HttpMethod.Post, "/api/plans/scenario", input, HttpStatusCode.BadRequest);
        Assert.Empty((await WorkspaceFlowTests.Send(client, HttpMethod.Get, "/api/tasks")).GetProperty("data").EnumerateArray());
    }
}
