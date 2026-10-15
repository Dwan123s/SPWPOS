using SPWPOS.Application.Workspace;

namespace SPWPOS.TestSupport;

// Canned AI response for workflow tests; never contacts a provider or parses note text.
public sealed class NoteAiTestDouble(int count = 1, bool needsReview = false, string source = "Gemini") : ITaskInterpreter
{
    public NoteAgentCapabilities Capabilities => new(true, Provider: source);
    public bool? LastUseAi { get; private set; }
    public Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string timeZoneId, DateTimeOffset now, CancellationToken ct)
    {
        LastUseAi = useAi;
        TaskInput[] tasks = count == 2
            ? [new("Báo cáo", "", now.AddDays(2), 120, TaskType: "WRITING"), new("Ôn bài", "", now.AddDays(2), 90, TaskType: "STUDY")]
            : [new("Báo cáo", "", now.AddDays(2), 60, TaskType: "WRITING")];
        return Task.FromResult(new NoteAnalysis(tasks, source, [], needsReview));
    }
    public Task<TaskSuggestion> CaptureAsync(CaptureInput input, string timeZoneId, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<BreakdownView> BreakdownAsync(TaskInput input, bool useAi, CancellationToken ct) => throw new NotSupportedException();
}
