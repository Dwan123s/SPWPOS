using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Persistence;
using SPWPOS.TestSupport;
using static SPWPOS.IntegrationTests.WorkspaceFlowTests;

namespace SPWPOS.IntegrationTests;

public sealed class ContinuousActivityApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<DateTimeOffset> Setup(ApiFactory factory, HttpClient client, bool fragmented)
    {
        await Authorize(factory, client);
        await Send(client, HttpMethod.Put, "/api/time/preferences", new PreferenceInput("UTC", 25, 240, 0, 540, 1080));
        await Send(client, HttpMethod.Put, "/api/time/availability", new WeeklyInput(
            Enumerable.Range(0, 7).SelectMany(day => fragmented
                ? new[] { new AvailabilityInput(day, 540, 600), new AvailabilityInput(day, 660, 720) }
                : new[] { new AvailabilityInput(day, 540, 660) }).ToArray()));
        return new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(9), TimeSpan.Zero);
    }

    [Theory]
    [InlineData("Cuộc họp nhóm", "OTHER")]
    [InlineData("Họp với khách hàng", "MEETING")]
    [InlineData("Phỏng vấn ứng viên", "OTHER")]
    public async Task Creating_or_editing_a_meeting_cannot_enable_splitting_to_fit_fragmented_availability(string name, string type)
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var start = await Setup(factory, client, true);
        var input = new TaskInput(name, "", start.AddHours(3), 120, TaskType: type, Splittable: true, IsHardDeadline: true);
        var id = (await Send(client, HttpMethod.Post, "/api/tasks", input)).GetProperty("data").GetProperty("id").GetGuid();
        var task = (await Send(client, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("data");
        Assert.False(task.GetProperty("splittable").GetBoolean());
        Assert.True(task.GetProperty("requiresContinuousSession").GetBoolean());
        await Send(client, HttpMethod.Put, $"/api/tasks/{id}", input with { Version = task.GetProperty("version").GetGuid() });
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(start, 1, UseCapacityFactor: false, UseEstimateBias: false))).GetProperty("data");
        Assert.Empty(plan.GetProperty("sessions").EnumerateArray());
        Assert.Contains(plan.GetProperty("warnings").EnumerateArray(), warning => warning.GetProperty("code").GetString() == "NO_CONTIGUOUS_SLOT");
        await using var scope = factory.Services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WorkTasks.SingleAsync(Ct);
        Assert.False(saved.Splittable);
    }

    [Fact]
    public async Task A_meeting_can_be_planned_whole_but_neither_split_nor_shortened_even_for_legacy_task_flags()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var start = await Setup(factory, client, false);
        var id = (await Send(client, HttpMethod.Post, "/api/tasks", new TaskInput("Họp nhóm", "", start.AddHours(3), 120,
            TaskType: "OTHER", IsHardDeadline: true))).GetProperty("data").GetProperty("id").GetGuid();
        var plan = (await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(start, 1, UseCapacityFactor: false, UseEstimateBias: false))).GetProperty("data");
        var session = Assert.Single(plan.GetProperty("sessions").EnumerateArray());
        Assert.Equal(120, (session.GetProperty("end").GetDateTimeOffset() - session.GetProperty("start").GetDateTimeOffset()).TotalMinutes);
        await Send(client, HttpMethod.Post, $"/api/plans/{plan.GetProperty("id").GetGuid()}/apply");
        // Emulate an older record that incorrectly allowed splitting.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.WorkTasks.SingleAsync(Ct)).Splittable = true;
            await db.SaveChangesAsync(Ct);
        }
        var sid = session.GetProperty("id").GetGuid();
        var split = await Send(client, HttpMethod.Post, $"/api/sessions/{sid}/split", new SplitInput(60), HttpStatusCode.BadRequest);
        Assert.Equal("CANNOT_SPLIT", split.GetProperty("error").GetProperty("code").GetString());
        var edit = await Send(client, HttpMethod.Put, $"/api/sessions/{sid}", new SessionEdit(start, start.AddHours(1)), HttpStatusCode.Conflict);
        Assert.Equal("CONTINUOUS_ACTIVITY_REQUIRED", edit.GetProperty("error").GetProperty("code").GetString());
        var workspace = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        var saved = Assert.Single(workspace.GetProperty("sessions").EnumerateArray());
        Assert.Equal(120, (saved.GetProperty("end").GetDateTimeOffset() - saved.GetProperty("start").GetDateTimeOffset()).TotalMinutes);
    }

    [Fact]
    public async Task Planning_keeps_a_fixed_meeting_as_one_event_with_exact_original_times()
    {
        await using var factory = new ApiFactory(); using var client = await factory.ClientAsync();
        var start = await Setup(factory, client, true);
        await Send(client, HttpMethod.Post, "/api/time/events", new EventInput("Họp dự án", start, start.AddHours(2), EventType: "MEETING"));
        await Send(client, HttpMethod.Post, "/api/tasks", new TaskInput("Viết báo cáo", "", start.AddHours(3), 120, TaskType: "WRITING", IsHardDeadline: true));
        await Send(client, HttpMethod.Post, "/api/plans", new PlanInput(start, 1, UseCapacityFactor: false, UseEstimateBias: false));
        var workspace = (await Send(client, HttpMethod.Get, "/api/workspace")).GetProperty("data");
        var meeting = Assert.Single(workspace.GetProperty("events").EnumerateArray());
        Assert.Equal(start, meeting.GetProperty("start").GetDateTimeOffset());
        Assert.Equal(start.AddHours(2), meeting.GetProperty("end").GetDateTimeOffset());
        Assert.Empty(workspace.GetProperty("sessions").EnumerateArray());
    }
}
