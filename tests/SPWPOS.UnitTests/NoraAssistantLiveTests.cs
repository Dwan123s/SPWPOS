using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

// Opt-in calls use only fictional prompts and an isolated in-memory account.
public sealed class NoraAssistantLiveTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_ASSISTANT_TESTS") == "1";
    [Fact(SkipUnless = nameof(Enabled), Skip = "Opt-in real 3.8 Flash assistant behavior and latency check.")]
    public async Task Assistant_understands_before_tools_and_handles_chat_advice_reads_and_a_complete_action()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT")!;
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "src/SPWPOS.Api/appsettings.json"))
            .AddUserSecrets("SPWPOS-Authentication-Development").Build();
        Assert.Equal("gemini-3.8-flash", config["AiSettings:Model"]);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(55) };
        var model = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        Assert.True(model.SupportsRequestUnderstanding);
        await using var h = await ServiceHarness.CreateAsync(s => { s.AddSingleton<ITaskInterpreter>(model); s.Configure<NoraAgentOptions>(x => x.Mode = "Agent"); });
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-04T01:00:00Z") - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync();
        await using var scope = h.Provider.CreateAsyncScope();
        var work = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        await work.SetPreferenceAsync(user.Id, new("Asia/Ho_Chi_Minh", 25, 90, 10, 480, 1080), ct);
        await work.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(d => new AvailabilityInput(d, 480, 1320)).ToArray()), ct);
        await work.SaveEventAsync(user.Id, null, new("[TEST] Học cố định", DateTimeOffset.Parse("2026-11-05T08:00:00+07:00"), DateTimeOffset.Parse("2026-11-05T10:00:00+07:00"), EventType: "CLASS"), ct);
        var agent = scope.ServiceProvider.GetRequiredService<IPlanningAgentService>();
        var measurements = new List<object>();
        async Task<AgentTurnView> Send(string name, string text)
        {
            var watch = Stopwatch.StartNew();
            var result = await agent.SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), text, false), ct);
            var errors = result.Run is null ? [] : await h.DbAsync(db => db.AgentToolCalls.Where(t => t.RunId == result.Run.Id && t.Status == "ERROR").Select(t => t.ResultCode).ToArrayAsync(ct));
            measurements.Add(new { name, milliseconds = watch.ElapsedMilliseconds, status = result.Status,
                intent = result.Run?.Understanding?.Intent, modelCalls = result.Run?.ModelTurns,
                tools = result.Run?.Steps.Select(s => s.Tool).ToArray(), toolCalls = result.Run?.ToolCalls, errors });
            await File.WriteAllTextAsync(Path.Combine(root, "artifacts/nora-assistant-live-measurements.json"),
                JsonSerializer.Serialize(new { model = config["AiSettings:Model"], measurements }), ct);
            Console.WriteLine($"ASSISTANT {name}: {watch.ElapsedMilliseconds} ms, {result.Status}, model={result.Run?.ModelTurns}, tools={result.Run?.ToolCalls}");
            return result;
        }
        var hello = await Send("greeting", "Chào NORA, bạn có thể giúp tôi những gì?");
        Assert.Equal("ANSWERED", hello.Status); Assert.Equal(0, hello.Run!.ToolCalls); Assert.Equal(1, hello.Run.ModelTurns);
        var advice = await Send("advice", "Tôi thấy nhiều bài quá nên hơi nản, bạn có lời khuyên để tôi bắt đầu không? Tôi chỉ muốn trao đổi, chưa lên lịch.");
        Assert.Equal("ANSWERED", advice.Status); Assert.Equal(0, advice.Run!.ToolCalls); Assert.Equal(1, advice.Run.ModelTurns);
        var how = await Send("explanation", "Giải thích ma trận Eisenhower hoạt động như thế nào, tôi chỉ hỏi cách dùng thôi.");
        Assert.Equal("ANSWERED", how.Status); Assert.Equal(0, how.Run!.ToolCalls);
        var missing = await Send("clarification", "Giúp tôi đặt một cuộc họp nhóm, tôi chưa xác định ngày và giờ.");
        Assert.Equal("NEEDS_INPUT", missing.Status); Assert.DoesNotContain(missing.Run!.Steps, s => s.Tool is "get_calendar" or "find_free_slots" or "simulate_plan");
        var calendar = await Send("calendar", "Cho tôi xem lịch ngày mai 05/11/2026, chỉ xem.");
        Assert.Equal("ANSWERED", calendar.Status); Assert.Contains("[TEST] Học cố định", calendar.Reply); Assert.Equal(1, calendar.Run!.ModelTurns);
        var task = await Send("task", "Tạo công việc [TEST] Viết báo cáo, cần 60 phút, hạn chót 12:00 trưa ngày mai 05/11/2026. Giữ nguyên lịch học, chỉ xem trước.");
        Assert.Equal("PREVIEW", task.Status); Assert.NotEmpty(task.Plan!.Sessions); Assert.InRange(task.Run!.ModelTurns, 1, 2);
        Assert.NotNull(task.Run.Understanding);
    }
}
