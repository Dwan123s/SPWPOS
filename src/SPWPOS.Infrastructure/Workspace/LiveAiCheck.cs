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
        var request = $"Tối thứ Năm {day:dd'/'MM'/'yyyy} mình muốn dành thời gian ôn tiếng Anh. Bạn xếp giúp mình nhé.";
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
        var appointment = $"Ngày {day:dd'/'MM'/'yyyy} mình có hẹn với thầy lúc 6 giờ, kéo dài khoảng nửa tiếng. Thêm vào lịch giúp mình nhé.";
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
            activeTaskTypes = new[] { "OTHER", "WRITING" },
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
    public static async Task RunFreeTimeAsync(ITaskInterpreter interpreter, TimeProvider clock, CancellationToken ct)
    {
        if (interpreter is not IPlanningAgentInterpreter agent || !agent.Capabilities.AiAvailable)
            throw new InvalidOperationException("AI is not configured.");
        const string zoneId = "Asia/Ho_Chi_Minh";
        var zone = PlanningEngine.Zone(zoneId); var now = clock.GetUtcNow();
        var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).AddDays(1);
        var dayStart = PlanningEngine.At(tomorrow, 0, zone);
        var dayEnd = PlanningEngine.At(tomorrow.AddDays(1), 0, zone);
        DateTimeOffset Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, zone);
        var freeContext = JsonSerializer.Serialize(new {
            activeTaskTypes = new[] { "OTHER" },
            clock = new { currentLocal = Local(now), today = tomorrow.AddDays(-1), tomorrow },
            tasks = Array.Empty<object>(), sessions = Array.Empty<object>(), dependencies = Array.Empty<object>(), events = Array.Empty<object>(),
            availability = new[] { new AvailabilityInput((int)tomorrow.DayOfWeek, 1140, 1320) },
            exceptions = new[] { new { date = tomorrow, startMinute = 1200, endMinute = 1260, isAvailable = false } },
            verifiedFreeTime = new { rangeStart = Local(now), rangeEnd = Local(now.AddDays(30)), timeZoneId = zoneId,
                includesAllSavedCalendarItems = true, intervals = new[] {
                    new { date = tomorrow, start = Local(PlanningEngine.At(tomorrow, 1140, zone)), end = Local(PlanningEngine.At(tomorrow, 1200, zone)), minutes = 60 },
                    new { date = tomorrow, start = Local(PlanningEngine.At(tomorrow, 1260, zone)), end = Local(PlanningEngine.At(tomorrow, 1320, zone)), minutes = 60 } } },
            preference = new { maximumDailyMinutes = 480, maximumSessionMinutes = 240, minimumSessionMinutes = 25, breakMinutes = 0, planningHorizonDays = 14 }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var free = await agent.InterpretAsync("Ngày mai tôi có thể làm việc trong khoảng giờ rảnh nào? Chỉ xem giờ rảnh trong cài đặt và các mục lịch đã lưu, chưa thay đổi lịch.",
            freeContext, [], zoneId, now, ct);
        Check(free.Question.Length == 0 && free.FreeTimeQuery is { } query && query.Start == dayStart && query.End == dayEnd &&
            free.Events.Length == 0 && free.Tasks.Length == 0 && free.Exceptions.Length == 0 && free.WeeklySlots.Length == 0 &&
            free.ScheduleTaskIds.Length == 0 && !free.Reschedule && !free.ReplaceWeeklyAvailability,
            "AI did not return an exact read-only free-time query for tomorrow.");
        Console.WriteLine("LIVE AI: free-time question resolved to the correct local day without calendar mutations; verified gaps are 19:00-20:00 and 21:00-22:00.");
        Console.WriteLine($"LIVE AI: {agent.Capabilities.Provider} free-time check passed. No calendar data was saved.");
    }
    public static async Task RunUrgencyAsync(ITaskInterpreter interpreter, TimeProvider clock, CancellationToken ct)
    {
        if (interpreter is not IPlanningAgentInterpreter agent || !agent.Capabilities.AiAvailable)
            throw new InvalidOperationException("AI is not configured.");
        const string zoneId = "Asia/Ho_Chi_Minh";
        var now = clock.GetUtcNow(); var zone = PlanningEngine.Zone(zoneId);
        var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).AddDays(1);
        var oldId = Guid.NewGuid();
        var context = JsonSerializer.Serialize(new {
            activeTaskTypes = new[] { "OTHER", "WRITING" },
            tasks = new[] { new { id = oldId, name = "Bài tập đã lên lịch", priority = 2, estimatedMinutes = 60,
                deadline = PlanningEngine.At(tomorrow.AddDays(7), 1320, zone), isHardDeadline = true } },
            sessions = new[] { new { id = Guid.NewGuid(), taskId = oldId, start = PlanningEngine.At(tomorrow, 1140, zone),
                end = PlanningEngine.At(tomorrow, 1200, zone), status = "Scheduled", isLocked = false } },
            dependencies = Array.Empty<object>(), events = Array.Empty<object>(), exceptions = Array.Empty<object>(),
            availability = Enumerable.Range(0, 7).Select(day => new AvailabilityInput(day, 1140, 1320)).ToArray(),
            preference = new { maximumDailyMinutes = 480, maximumSessionMinutes = 240, minimumSessionMinutes = 25, breakMinutes = 0, planningHorizonDays = 14 }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var request = $"Có báo cáo đột xuất rất khẩn, cần làm liên tục 60 phút từ 19:00 đến 20:00 ngày {tomorrow:yyyy-MM-dd}, hạn chót bắt buộc 20:00 cùng ngày. Ưu tiên báo cáo này và dời bài tập đang trùng giờ giúp tôi, giữ nguyên hạn chót của bài tập.";
        var result = await agent.InterpretAsync(request, context, [], zoneId, now, ct);
        Check(result.Question.Length == 0 && result.Tasks.Length == 1 && result.Events.Length == 0 &&
            result.Tasks[0].Priority == 4 && result.Tasks[0].EstimatedMinutes == 60 &&
            result.Tasks[0].Deadline == PlanningEngine.At(tomorrow, 1200, zone) &&
            result.Tasks[0].WorkWindowStart == PlanningEngine.At(tomorrow, 1140, zone) &&
            result.Tasks[0].WorkWindowEnd == PlanningEngine.At(tomorrow, 1200, zone),
            "AI did not preserve urgent priority, duration, deadline and requested work window.");
        Console.WriteLine($"LIVE AI: {agent.Capabilities.Provider} recognizes urgent work at priority 4 with exact duration, deadline and work window. No calendar data was saved.");
    }
    public static async Task RunScheduleReviewAsync(ITaskInterpreter interpreter, TimeProvider clock, CancellationToken ct)
    {
        if (interpreter is not IPlanningAgentInterpreter agent || !agent.Capabilities.AiAvailable)
            throw new InvalidOperationException("AI is not configured.");
        const string zoneId = "Asia/Ho_Chi_Minh";
        var zone = PlanningEngine.Zone(zoneId); var now = clock.GetUtcNow();
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).AddDays(1);
        DateTimeOffset At(int minute) => TimeZoneInfo.ConvertTime(PlanningEngine.At(day, minute, zone), zone);
        var query = new AgentFreeTimeQuery(At(0), At(1440));
        var request = "Phân tích lịch trình ngày mai của tôi và tư vấn nên ưu tiên việc gì, chưa thay đổi lịch.";
        var gaps = new[] { (810, 820), (850, 860), (990, 1000), (1030, 1040), (1070, 1140) }
            .Select(x => new { start = At(x.Item1), end = At(x.Item2), minutes = x.Item2 - x.Item1 }).ToArray();
        var entries = new[] { ("Họp nhóm", 820, 850), ("Đi học", 860, 990), ("Trao đổi dự án", 1000, 1030), ("Cuộc hẹn", 1040, 1070) }
            .Select(x => new { name = x.Item1, start = At(x.Item2), end = At(x.Item3), kind = "EVENT", status = "FIXED" }).ToArray();
        var task = new { name = "Báo cáo gần hạn", deadline = At(1200), priority = 4, remainingMinutes = 60, savedMinutes = 0,
            concern = "Chưa có đủ thời lượng trong các phiên phù hợp trước hạn." };
        var facts = new { query, asOf = TimeZoneInfo.ConvertTime(now, zone), timeZoneId = zoneId, hasAvailability = true,
            metrics = new { fixedEvents = 4, workSessions = 0, occupiedMinutes = 220, remainingCalendarFreeMinutes = 110,
                shortGapCount = 4, minimumSessionMinutes = 25, schedulableMinutesWithinDailyBudgets = 110 },
            entries, entryScope = new { included = 4, total = 4, truncated = false }, freeGaps = gaps, conflicts = Array.Empty<string>(),
            tasks = new[] { task }, taskScope = new { included = 1, total = 1, truncated = false },
            taskSuggestions = new[] { new { name = task.name, start = At(1070), end = At(1130), minutes = 60 } },
            taskSuggestionsAreAlternatives = true, changesSaved = false };
        string Context(bool verified) => JsonSerializer.Serialize(new {
            activeTaskTypes = new[] { "OTHER", "WRITING" },
            clock = new { currentLocal = TimeZoneInfo.ConvertTime(now, zone), today = day.AddDays(-1), tomorrow = day },
            tasks = new[] { task }, events = entries, sessions = Array.Empty<object>(), exceptions = Array.Empty<object>(), dependencies = Array.Empty<object>(),
            availability = new[] { new AvailabilityInput((int)day.DayOfWeek, 810, 1140) },
            verifiedFreeTime = new { rangeStart = At(0), rangeEnd = At(1440), intervals = gaps },
            verifiedScheduleReview = verified ? facts : null
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var scope = await agent.InterpretAsync(request, Context(false), [], zoneId, now, ct);
        Console.WriteLine($"LIVE AI REVIEW SCOPE: review={scope.ScheduleReviewQuery}; freeTime={scope.FreeTimeQuery}; actions={HasActions(scope)}.");
        Check(scope.ScheduleReviewQuery == query && scope.FreeTimeQuery is null && !HasActions(scope), "Schedule analysis must use its own read-only query.");
        var advice = await agent.InterpretAsync(request, Context(true), [], zoneId, now, ct);
        Console.WriteLine($"LIVE AI REVIEW ADVICE: characters={advice.Reply.Length}; review={advice.ScheduleReviewQuery}; actions={HasActions(advice)}.");
        Console.WriteLine("FICTIONAL SCHEDULE ADVICE:\n" + advice.Reply);
        Check(advice.ScheduleReviewQuery == query && advice.FreeTimeQuery is null && !HasActions(advice) &&
            advice.Reply.Length is > 400 and <= 2200 && advice.Reply.Contains("báo cáo", StringComparison.OrdinalIgnoreCase) &&
            advice.Reply.Contains("17:50", StringComparison.Ordinal) && advice.Reply.Contains("18:50", StringComparison.Ordinal),
            "Schedule advice must be detailed, grounded in the saved work, and read-only.");
        Console.WriteLine($"LIVE AI: {agent.Capabilities.Provider} distinguishes schedule analysis from a free-time lookup and gives detailed advice from verified facts. No calendar data saved.");
        static bool HasActions(AgentIntent intent) => intent.Question.Length > 0 || intent.ReplaceWeeklyAvailability || intent.Reschedule ||
            intent.Tasks.Length > 0 || intent.Events.Length > 0 || intent.Exceptions.Length > 0 || intent.WeeklySlots.Length > 0 || intent.ScheduleTaskIds.Length > 0;
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
