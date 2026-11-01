using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Workspace;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

// Opt-in real-provider checks use only fictional data in a disposable in-memory database.
public sealed class AiLatencyLiveTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PLANORA_LIVE_LATENCY_TESTS") == "1";
    [Fact(SkipUnless = nameof(Enabled), Skip = "Opt-in real-provider latency checks.")]
    public async Task Fast_model_preserves_validation_and_reads_timetable_in_a_bounded_number_of_calls()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Environment.GetEnvironmentVariable("PLANORA_REPOSITORY_ROOT")!;
        var modelName = Environment.GetEnvironmentVariable("PLANORA_LATENCY_MODEL") ?? "gemini-3.1-flash-lite";
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "src/SPWPOS.Api/appsettings.json"))
            .AddUserSecrets("SPWPOS-Authentication-Development").AddInMemoryCollection(new Dictionary<string, string?> { ["AiSettings:Model"] = modelName }).Build();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(65) };
        var model = new TaskInterpreter(http, config, NullLogger<TaskInterpreter>.Instance);
        Assert.True(model.AgentAvailable);
        await using var h = await ServiceHarness.CreateAsync(s => { s.AddSingleton<ITaskInterpreter>(model); s.Configure<NoraAgentOptions>(x => x.Mode = "Agent"); });
        h.Clock.Advance(DateTimeOffset.Parse("2026-11-01T06:00:00Z") - h.Clock.GetUtcNow());
        var user = await h.RegisterAsync();
        var measurements = new List<object>();
        async Task<AgentTurnView> Send(string text, AgentImage? image = null)
        {
            await using var scope = h.Provider.CreateAsyncScope(); var watch = Stopwatch.StartNew();
            var result = await scope.ServiceProvider.GetRequiredService<IPlanningAgentService>().SendAgentTurnAsync(user.Id, new(Guid.NewGuid(), Guid.NewGuid(), text, false, Image: image), ct);
            Console.WriteLine($"AI LATENCY model={modelName}; image={image is not null}; ms={watch.ElapsedMilliseconds}; status={result.Status}; modelCalls={result.Run?.ModelTurns}; tools={string.Join(',', result.Run?.Steps.Select(s => s.Tool) ?? [])}");
            var codes = await h.DbAsync(db => db.AgentToolCalls.Where(x => x.RunId == result.Run!.Id).OrderBy(x => x.Sequence).Select(x => x.ResultCode).ToArrayAsync(ct));
            measurements.Add(new { image = image is not null, milliseconds = watch.ElapsedMilliseconds, status = result.Status, result.ErrorCode, modelCalls = result.Run?.ModelTurns, tools = result.Run?.Steps.Select(s => s.Tool).ToArray(), codes });
            await File.WriteAllTextAsync(Path.Combine(root, "artifacts/ai-latency-live-measurements.json"), JsonSerializer.Serialize(new { modelName, measurements }), ct);
            return result;
        }
        await using (var scope = h.Provider.CreateAsyncScope()) {
            var work = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
            await work.SetPreferenceAsync(user.Id, new("Asia/Ho_Chi_Minh", 25, 180, 0, 480, 1320, MaximumDailyMinutes: 480), ct);
            await work.SetAvailabilityAsync(user.Id, new(Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 480, 1320)).ToArray()), ct);
            await work.SaveEventAsync(user.Id, null, new("Lịch học cố định", DateTimeOffset.Parse("2026-11-02T08:00:00+07:00"), DateTimeOffset.Parse("2026-11-02T10:00:00+07:00"), EventType: "CLASS"), ct);
        }
        var read = await Send("Cho tôi xem lịch ngày mai 02/11/2026, chỉ xem.");
        Assert.Equal("ANSWERED", read.Status); Assert.Contains("Lịch học cố định", read.Reply); Assert.InRange(read.Run!.ModelTurns, 1, 3);
        var task = await Send("Tạo công việc Làm slide, cần 90 phút liên tục vào ngày 02/11/2026, deadline 12:00 trưa ngày 02/11/2026. Giữ nguyên lịch học cố định.");
        Assert.Equal("PREVIEW", task.Status); var slot = Assert.Single(task.Plan!.Sessions, x => !x.Preserved);
        Assert.Equal(90, (slot.End - slot.Start).TotalMinutes); Assert.True(slot.Start >= DateTimeOffset.Parse("2026-11-02T10:00:00+07:00"));
        Assert.True(slot.End <= DateTimeOffset.Parse("2026-11-02T12:00:00+07:00")); Assert.InRange(task.Run!.ModelTurns, 1, 4);
        Assert.Empty(await h.DbAsync(db => db.WorkTasks.ToArrayAsync(ct)));
        // Remove only the fictional test event so the image test has no conflicting commitments.
        await h.DbAsync(async db => { db.FixedEvents.RemoveRange(await db.FixedEvents.ToArrayAsync(ct)); await db.SaveChangesAsync(ct); });
        var bytes = await File.ReadAllBytesAsync(Path.Combine(root, "tests/SPWPOS.UnitTests/Fixtures/ai-latency-timetable.png"), ct);
        var image = new AgentImage("fictional-week.png", "image/png", Convert.ToBase64String(bytes));
        var imported = await Send("Nhập thời khóa biểu trong ảnh cho đúng tuần 02/11/2026–08/11/2026, chỉ một tuần. Giữ nguyên tất cả tám buổi học. Hãy tạo đề xuất để tôi xác nhận.", image);
        Assert.Equal("PREVIEW", imported.Status); Assert.Equal(8, imported.Changes.Count(x => x.Kind == "EVENT")); Assert.InRange(imported.Run!.ModelTurns, 1, 3);
        Assert.Empty(await h.DbAsync(db => db.FixedEvents.ToArrayAsync(ct)));
        var watch = Stopwatch.StartNew();
        var note = await model.AnalyzeNoteAsync("Ngày 02/11/2026 họp nhóm từ 14:00 đến 15:00. Viết báo cáo cần 60 phút, hạn chót 20:00 ngày 03/11/2026.", true, "Asia/Ho_Chi_Minh", h.Clock.GetUtcNow(), ct);
        Assert.NotEmpty(note.Activities ?? []); Assert.Contains(note.Activities!, x => x.Name.Contains("báo cáo", StringComparison.OrdinalIgnoreCase));
        measurements.Add(new { operation = "notes", milliseconds = watch.ElapsedMilliseconds, activities = note.Activities!.Length });
        watch.Restart();
        var policy = await model.DraftPolicyAsync("Tôi muốn đi ngủ lúc 22:00, giữ nguyên các giờ khác.", new(420, 1380, 720, 750, 1080, 1110, 30, 45, false), ct);
        Assert.Equal(1320, policy.AwakeEnd); Assert.Equal(720, policy.LunchStart);
        measurements.Add(new { operation = "routine-policy", milliseconds = watch.ElapsedMilliseconds });
        await File.WriteAllTextAsync(Path.Combine(root, "artifacts/ai-latency-live-measurements.json"), JsonSerializer.Serialize(new { modelName, measurements }), ct);
        await File.WriteAllTextAsync(Path.Combine(root, "artifacts/ai-latency-live-result.json"), JsonSerializer.Serialize(new { modelName, read = read.Run.ModelTurns, task = task.Run.ModelTurns, image = imported.Run.ModelTurns, imageEvents = imported.Changes.Count(x => x.Kind == "EVENT"), allValidated = true }), ct);
    }
}
