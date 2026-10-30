using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

// Explicit opt-in: real provider calls with fictional calendar data in an
// in-memory database. Never reads or writes a user's actual calendar.
public sealed class NoraLiveAgentTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_AGENT_TESTS") == "1";
    [Fact(SkipUnless = nameof(Enabled), Skip = "Opt-in real provider smoke test.")]
    public async Task Native_provider_completes_a_real_tool_loop_and_keeps_the_calendar_a_draft()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT") ?? Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "src", "SPWPOS.Api", "appsettings.json"))
            .AddUserSecrets("SPWPOS-Authentication-Development").Build();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(50) };
        var model = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        Assert.True(model.AgentAvailable);
        await using var h = await ServiceHarness.CreateAsync(s => { s.AddSingleton<ITaskInterpreter>(model);
            s.Configure<NoraAgentOptions>(x => x.Mode = "Agent"); });
        var user = await h.RegisterAsync();
        await using var scope = h.Provider.CreateAsyncScope();
        var work = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await work.SetPreferenceAsync(user.Id, new("Asia/Ho_Chi_Minh", 25, 180, 0, 480, 1320, MaximumDailyMinutes: 480), ct);
        await work.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(d => new AvailabilityInput(d, 480, 1320)).ToArray()), ct);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(h.Clock.GetUtcNow(), PlanningEngine.Zone("Asia/Ho_Chi_Minh")).DateTime).AddDays(1);
        var start = PlanningEngine.At(day, 480, PlanningEngine.Zone("Asia/Ho_Chi_Minh"));
        await work.SaveEventAsync(user.Id, null, new("[PROBE] Lịch học", start, start.AddHours(2), EventType: "CLASS"), ct);
        var agent = scope.ServiceProvider.GetRequiredService<IPlanningAgentService>();
        var events = new List<AgentStreamEvent>();
        var result = await agent.SendAgentTurnStreamingAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(),
            $"Tạo công việc mới tên [PROBE] Làm slide, cần 90 phút liên tục vào ngày mai {day:dd/MM/yyyy}, hạn chót 12:00 trưa ngày {day:dd/MM/yyyy}. Giữ nguyên lịch học.", false),
            (item, _) => { events.Add(item); return Task.CompletedTask; }, ct);
        Assert.Equal("PREVIEW", result.Status); Assert.Equal("Agent", result.Run!.Mode);
        Assert.Contains(result.Run.Steps, x => x.Tool == "simulate_plan");
        Assert.Equal("finish_proposal", result.Run.Steps[^1].Tool);
        var scheduled = Assert.Single(result.Plan!.Sessions, x => !x.Preserved);
        Assert.Equal(90, (scheduled.End - scheduled.Start).TotalMinutes);
        Assert.True(scheduled.Start >= start.AddHours(2)); Assert.True(scheduled.End <= start.AddHours(4));
        Assert.Equal(0, await h.DbAsync(db => db.WorkTasks.CountAsync(ct)));
        Assert.Equal(0, await h.DbAsync(db => db.WorkSessions.CountAsync(ct)));
        Console.WriteLine($"LIVE AGENT: {result.Status}; {result.Run.ModelTurns} model turns; {result.Run.ToolCalls} tool calls: {string.Join(", ", result.Run.Steps.Select(x => x.Tool))}. No real calendar data accessed.");
    }
}
