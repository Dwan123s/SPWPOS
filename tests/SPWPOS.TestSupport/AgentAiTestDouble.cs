using SPWPOS.Application.Workspace;
namespace SPWPOS.TestSupport;

public sealed class AgentAiTestDouble : ITaskInterpreter, IPlanningAgentInterpreter, ITeamBriefInterpreter, IImportanceInterpreter
{
    public NoteAgentCapabilities Capabilities => new(true, Provider: "Gemini");
    public Func<DateTimeOffset, AgentIntent> Response { get; set; } = _ => Empty();
    public Func<Task>? BeforeReply { get; set; }
    public Func<Func<string, CancellationToken, Task>, CancellationToken, Task<AgentIntent>>? StreamResponse { get; set; }
    public int Calls { get; private set; }
    public string Context { get; private set; } = "";
    public AgentMessage[] History { get; private set; } = [];
    public AgentImage? LastImage { get; private set; }
    public int ImageCalls { get; private set; }
    public TeamBriefContext? BriefContext { get; private set; }
    public Func<TeamBriefContext, TeamBriefAnalysis>? BriefResponse { get; set; }
    public ImportanceContext? ImportanceContext { get; private set; }
    public Func<ImportanceContext, ImportanceSuggestion>? ImportanceResponse { get; set; }
    public async Task<ImportanceSuggestion> SuggestImportanceAsync(ImportanceContext context, CancellationToken ct)
    {
        Calls++; ImportanceContext = context;
        if (BeforeReply != null) await BeforeReply();
        return ImportanceResponse?.Invoke(context) ?? new("Unknown", "Cần làm rõ ảnh hưởng", .5, "");
    }
    public async Task<TeamBriefAnalysis> AnalyzeTeamBriefAsync(TeamBriefContext context, CancellationToken ct)
    {
        Calls++; BriefContext = context;
        if (BeforeReply != null) await BeforeReply();
        return BriefResponse?.Invoke(context) ?? new("Chưa đủ thông tin", "Cần deadline", "", [], []);
    }
    public async Task<AgentIntent> InterpretImageAsync(string text, string contextJson, AgentMessage[] history,
        string timeZoneId, DateTimeOffset now, AgentImage image, Func<string, CancellationToken, Task>? onReplyDelta, CancellationToken ct)
    {
        LastImage = image;
        ImageCalls++;
        var result = await InterpretAsync(text, contextJson, history, timeZoneId, now, ct);
        if (onReplyDelta is not null) await onReplyDelta(result.Reply, ct);
        return result;
    }
    public static AgentIntent Empty() => new("Đã đọc lịch hiện tại.", "", false, [], [], [], [], [], false, 14);
    public async Task<AgentIntent> InterpretStreamingAsync(string text, string contextJson, AgentMessage[] history,
        string timeZoneId, DateTimeOffset now, Func<string, CancellationToken, Task> onReplyDelta, CancellationToken ct)
    {
        if (StreamResponse is not null) return await StreamResponse(onReplyDelta, ct);
        var result = await InterpretAsync(text, contextJson, history, timeZoneId, now, ct);
        await onReplyDelta(result.Reply, ct);
        return result;
    }
    public async Task<AgentIntent> InterpretAsync(string text, string contextJson, AgentMessage[] history, string timeZoneId, DateTimeOffset now, CancellationToken ct)
    {
        Calls++; Context = contextJson; History = history;
        if (BeforeReply is not null) await BeforeReply();
        return Response(now);
    }
    public Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string zone, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<TaskSuggestion> CaptureAsync(CaptureInput input, string zone, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<BreakdownView> BreakdownAsync(TaskInput input, bool useAi, CancellationToken ct) => throw new NotSupportedException();
}
