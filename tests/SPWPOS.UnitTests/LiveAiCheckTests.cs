using SPWPOS.Application.Workspace;
using SPWPOS.Infrastructure.Workspace;

namespace SPWPOS.UnitTests;

public sealed class LiveAiCheckTests
{
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-31T06:00:00Z");
    }
    private sealed class Interpreter(bool legacy, bool invalidNote) : ITaskInterpreter, IPlanningAgentInterpreter
    {
        public NoteAgentCapabilities Capabilities => new(true, Provider: "VertexAI");
        public int Calls { get; private set; }
        private static DateTimeOffset At(int minute) => PlanningEngine.At(new(2026, 11, 1), minute, PlanningEngine.Zone("Asia/Ho_Chi_Minh"));
        public Task<AgentIntent> InterpretAsync(string text, string contextJson, AgentMessage[] history, string timeZoneId, DateTimeOffset now, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new AgentIntent("Đề xuất", "", false, [], [],
                Calls == 1 ? [new("Cuộc họp", At(840), At(960))] : [],
                Calls == 2 ? [new(null, "Báo cáo SWP", At(1080), 120, 2, "WRITING", false)] : [], [], false, 2));
        }
        public Task<NoteAnalysis> AnalyzeNoteAsync(string text, bool useAi, string timeZoneId, DateTimeOffset now, CancellationToken ct)
        {
            Calls++;
            var minutes = invalidNote ? 90 : 120;
            return Task.FromResult(legacy
                ? new NoteAnalysis([new("Báo cáo SWP", "", At(1080), minutes, TaskType: "WRITING", IsHardDeadline: true)], "VertexAI", [])
                : new NoteAnalysis([], "VertexAI", [], Activities: [new("TASK", "Báo cáo SWP", Type: "WRITING", EstimatedMinutes: minutes, Deadline: At(1080), IsHardDeadline: true)]));
        }
        public Task<TaskSuggestion> CaptureAsync(CaptureInput input, string timeZoneId, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<BreakdownView> BreakdownAsync(TaskInput input, bool useAi, CancellationToken ct) => throw new NotSupportedException();
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Live_check_validates_current_activities_and_legacy_notes_without_false_failures(bool legacy)
    {
        var interpreter = new Interpreter(legacy, false);
        await LiveAiCheck.RunAsync(interpreter, new Clock(), TestContext.Current.CancellationToken);
        Assert.Equal(3, interpreter.Calls);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Live_check_rejects_wrong_note_durations_in_both_formats(bool legacy)
    {
        var interpreter = new Interpreter(legacy, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => LiveAiCheck.RunAsync(interpreter, new Clock(), TestContext.Current.CancellationToken));
        Assert.Equal(3, interpreter.Calls);
    }
}
