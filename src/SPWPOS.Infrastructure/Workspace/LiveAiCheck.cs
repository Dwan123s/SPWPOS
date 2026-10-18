using System.Text.Json;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

// Opt-in diagnostics use fictional data and never write to the user's calendar.
public static class LiveAiCheck
{
    // A small opt-in live sample, not a production success-rate measurement.
    public static async Task RunQualityAsync(ITaskInterpreter interpreter, TimeProvider clock, CancellationToken ct)
    {
        if (interpreter is not IPlanningAgentInterpreter agent || !agent.Capabilities.AiAvailable) throw new InvalidOperationException("AI is not configured.");
        const string zoneId = "Asia/Ho_Chi_Minh";
        var now = clock.GetUtcNow(); var zone = PlanningEngine.Zone(zoneId);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).AddDays(1);
        while (day.DayOfWeek != DayOfWeek.Thursday) day = day.AddDays(1);
        var request = $"Tối thứ Năm {day:dd/MM/yyyy} mình muốn dành thời gian ôn tiếng Anh. Bạn xếp giúp mình nhé.";
        object Context(AgentPendingRequest? pending = null) => new {
            activeTaskTypes = new[] { "OTHER", "STUDY", "WRITING" }, pendingRequest = pending,
            tasks = Array.Empty<object>(), events = Array.Empty<object>(), exceptions = Array.Empty<object>(), sessions = Array.Empty<object>(), dependencies = Array.Empty<object>(),
            availability = new[] { new AvailabilityInput((int)day.DayOfWeek, 1140, 1320) },
            preference = new { maximumDailyMinutes = 480, maximumSessionMinutes = 240, minimumSessionMinutes = 25, breakMinutes = 0, planningHorizonDays = 14 }
        };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var missing = await agent.InterpretAsync(request, JsonSerializer.Serialize(Context(), json), [], zoneId, now, ct);
        Check(missing.ResultKind == "CLARIFICATION" && missing.Question.Length > 0 && missing.Tasks.Length == 0 && missing.Events.Length == 0 && !missing.Reschedule, "T09 must clarify without mutations.");
        Console.WriteLine("LIVE AI QUALITY: T09 missing duration asks a question with no mutations.");
        var followup = await agent.InterpretAsync("Khoảng 45 phút thôi, mình muốn học xong trước 21h.",
            JsonSerializer.Serialize(Context(new(request, null, PlanningEngine.At(day, 1080, zone), PlanningEngine.At(day, 1440, zone), null, null)), json),
            [new(request, missing.Reply + " " + missing.Question, "NEEDS_INPUT")], zoneId, now, ct);
        Check(followup.ResultKind == "MUTATION" && followup.Question.Length == 0 && followup.Tasks.Length == 1 && followup.Tasks[0].EstimatedMinutes == 45 &&
            followup.Tasks[0].Deadline == PlanningEngine.At(day, 1260, zone) && followup.Tasks[0].WorkWindowStart >= PlanningEngine.At(day, 1080, zone) &&
            followup.Tasks[0].WorkWindowStart < PlanningEngine.At(day, 1260, zone) && followup.Tasks[0].WorkWindowEnd <= PlanningEngine.At(day, 1440, zone), "T10 must retain Thursday and apply both duration and deadline.");
        Console.WriteLine("LIVE AI QUALITY: T10 keeps Thursday evening, 45 minutes and 21:00 deadline.");
        var appointment = $"Ngày {day:dd/MM/yyyy} mình có hẹn với thầy lúc 6 giờ, kéo dài khoảng nửa tiếng. Thêm vào lịch giúp mình nhé.";
        var ambiguous = await agent.InterpretAsync(appointment, JsonSerializer.Serialize(Context(), json), [], zoneId, now, ct);
        Check(ambiguous.ResultKind == "CLARIFICATION" && ambiguous.Question.Length > 0 && ambiguous.Events.Length == 0 && ambiguous.Tasks.Length == 0, "T11 must resolve AM/PM before writes.");
        Console.WriteLine("LIVE AI QUALITY: T11 deterministic input guard asks AM/PM before a provider request or event creation.");
        var clarified = await agent.InterpretAsync("À, 6 giờ chiều nhé.", JsonSerializer.Serialize(Context(new(appointment, null, null, null, null, null)), json),
            [new(appointment, ambiguous.Reply + " " + ambiguous.Question, "NEEDS_INPUT")], zoneId, now, ct);
        Check(clarified.ResultKind == "MUTATION" && clarified.Question.Length == 0 && clarified.Events.Length == 1 && clarified.Events[0].Start == PlanningEngine.At(day, 1080, zone) &&
            clarified.Events[0].End == PlanningEngine.At(day, 1110, zone), "T12 must create one clarified 18:00 appointment.");
        Console.WriteLine($"LIVE AI QUALITY: T12 resolves 18:00–18:30. {agent.Capabilities.Provider} quality checks passed (4 cases, 3 provider calls). No calendar data was saved.");
    }
    public static async Task RunVoiceAsync(IAgentVoiceTranscriber voice, string file, CancellationToken ct)
    {
        var mime = Path.GetExtension(file).ToLowerInvariant() switch { ".wav" => "audio/wav", ".webm" => "audio/webm", ".ogg" => "audio/ogg", ".mp4" => "audio/mp4", _ => "" };
        var info = new FileInfo(file);
        Check(voice.VoiceAvailable && info.Exists && info.Length is > 0 and <= VoiceLimits.MaxAudioBytes && mime.Length > 0, "Voice check configuration/file is invalid.");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await voice.TranscribeAsync(await File.ReadAllBytesAsync(file, ct), mime, ct);
        Check(result.Text.Length is > 0 and <= 2000, "Voice result is invalid.");
        Console.WriteLine($"LIVE AI VOICE: transcript validated; characters={result.Text.Length}; elapsed={watch.ElapsedMilliseconds}ms. Audio handled in memory. No calendar data saved.");
    }
    public static async Task RunStreamingAsync(ITaskInterpreter interpreter, TimeProvider clock, CancellationToken ct)
    {
        if (interpreter is not IPlanningAgentInterpreter agent || !agent.Capabilities.AiAvailable) throw new InvalidOperationException("AI is not configured.");
        var now = clock.GetUtcNow(); var zone = PlanningEngine.Zone("Asia/Ho_Chi_Minh");
        var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).AddDays(1);
        var context = """{"tasks":[],"events":[],"exceptions":[],"sessions":[],"dependencies":[],"availability":[],"preference":{"maximumDailyMinutes":480,"maximumSessionMinutes":240,"minimumSessionMinutes":25,"breakMinutes":0,"planningHorizonDays":14}}""";
        var deltas = new List<string>(); var stopwatch = System.Diagnostics.Stopwatch.StartNew(); var firstMs = 0L;
        var result = await agent.InterpretStreamingAsync($"Thêm cuộc họp thử nghiệm vào ngày {tomorrow:yyyy-MM-dd} từ 14:00 đến 16:00, chỉ một lần.", context, [], "Asia/Ho_Chi_Minh", now,
            (delta, _) => { if (deltas.Count == 0) firstMs = stopwatch.ElapsedMilliseconds; deltas.Add(delta); return Task.CompletedTask; }, ct);
        Check(result.Events.Length == 1 && result.Question.Length == 0 && result.Events[0].Start == PlanningEngine.At(tomorrow, 840, zone) && result.Events[0].End == PlanningEngine.At(tomorrow, 960, zone), "Stream did not preserve meeting time.");
        Check(deltas.Count > 0 && string.Concat(deltas) == result.Reply, "Streamed public reply did not match the validated result.");
        Console.WriteLine($"LIVE AI STREAM: {agent.Capabilities.Provider}; reply chunks={deltas.Count}; first delta={firstMs}ms; completed={stopwatch.ElapsedMilliseconds}ms. Schema and exact times verified. No calendar data saved.");
    }
    public static async Task RunAsync(ITaskInterpreter interpreter, TimeProvider clock, CancellationToken ct)
    {
        if (interpreter is not IPlanningAgentInterpreter agent || !agent.Capabilities.AiAvailable)
            throw new InvalidOperationException("AI is not configured.");
        const string zoneId = "Asia/Ho_Chi_Minh";
        var now = clock.GetUtcNow(); var zone = PlanningEngine.Zone(zoneId);
        var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).AddDays(1);
        var from = PlanningEngine.At(tomorrow, 840, zone); var to = PlanningEngine.At(tomorrow, 960, zone);
        var due = PlanningEngine.At(tomorrow, 1080, zone);
        var context = JsonSerializer.Serialize(new {
            tasks = Array.Empty<object>(), events = Array.Empty<object>(), exceptions = Array.Empty<object>(),
            sessions = Array.Empty<object>(), dependencies = Array.Empty<object>(),
            availability = new[] { new AvailabilityInput((int)tomorrow.DayOfWeek, 840, 960) },
            preference = new { maximumDailyMinutes = 480, maximumSessionMinutes = 240, minimumSessionMinutes = 25, breakMinutes = 0, planningHorizonDays = 14 }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var meeting = await agent.InterpretAsync($"Ngày {tomorrow:yyyy-MM-dd}, tôi có cuộc họp từ 14:00 đến 16:00. Thêm cuộc họp vào lịch cho tôi.", context, [], zoneId, now, ct);
        Check(meeting.Question.Length == 0 && meeting.Events.Length == 1 && meeting.Tasks.Length == 0 &&
            meeting.Events[0].Start == from && meeting.Events[0].End == to, "AI did not preserve the requested meeting time.");
        Console.WriteLine($"LIVE AI: meeting understood correctly ({tomorrow:yyyy-MM-dd}, 14:00-16:00, {zoneId}).");
        var report = await agent.InterpretAsync($"Tôi cần làm báo cáo SWP liên tục 120 phút, hạn chót bắt buộc là 18:00 ngày {tomorrow:yyyy-MM-dd}. Xếp vào giờ rảnh đã có cho tôi.", context, [], zoneId, now, ct);
        Check(report.Question.Length == 0 && report.Tasks.Length == 1 && report.Events.Length == 0 &&
            report.Tasks[0].Deadline == due && report.Tasks[0].EstimatedMinutes == 120 && report.Tasks[0].Splittable == false,
            "AI did not preserve the requested duration, continuity or deadline.");
        var task = report.Tasks[0]; var engine = new PlanningEngine();
        var planningState = new PlanningState([new WorkTask { Name = task.Name, EstimatedMinutes = 120, Deadline = due,
            Splittable = false, IsHardDeadline = true }], [],
            [new WeeklyAvailability { DayOfWeek = (int)tomorrow.DayOfWeek, StartMinute = 840, EndMinute = 960 }], [], [], [],
            new WorkPreference { TimeZoneId = zoneId, MaximumSessionMinutes = 240, MaximumDailyMinutes = 480, BreakMinutes = 0 });
        var plan = engine.Generate(planningState, new(now.AddMinutes(5), 2, UseCapacityFactor: false, UseEstimateBias: false), now);
        Check(plan.Warnings.Length == 0 && plan.Sessions.Length == 1 && plan.Sessions[0].Start == from && plan.Sessions[0].End == to,
            "The live AI request did not produce a feasible two-hour schedule.");
        Console.WriteLine("LIVE AI: SWP report scheduled for 120 continuous minutes before 18:00.");
        if (agent.Capabilities.Provider == "VertexAI")
        {
            var note = await interpreter.AnalyzeNoteAsync($"Viết báo cáo SWP trong 120 phút, hạn chót bắt buộc 18:00 ngày {tomorrow:yyyy-MM-dd}.",
                true, zoneId, now, ct);
            Check(note.Source == "VertexAI" && note.Tasks.Length == 1 && note.Tasks[0].EstimatedMinutes == 120 &&
                note.Tasks[0].Deadline == due && note.Tasks[0].IsHardDeadline, "Cloud note extraction did not preserve duration or deadline.");
            Console.WriteLine("LIVE AI: Google Cloud note extraction and source verified.");
        }
        Console.WriteLine($"LIVE AI: {agent.Capabilities.Provider} structured output and planner checks passed. No calendar data was saved.");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
