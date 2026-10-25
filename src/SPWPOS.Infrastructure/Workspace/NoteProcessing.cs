using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    // Kept independently of the latest analysis: saving a draft must never forget
    // which activities were committed, including tasks later renamed or deleted.
    private sealed record NoteProcessing(string Content, string[] TaskNames, string[] ActivityKeys,
        Guid[] PendingTaskIds, bool AnalysisImported, Guid? ImportVersion = null);
    private static string NoteName(string value) => Regex.Replace(value.Trim().Normalize(NormalizationForm.FormKC), @"\s+", " ").ToUpperInvariant();
    private static string ActivityKey(NoteActivity activity) => activity.Kind == "TASK"
        ? "TASK|" + NoteName(activity.Name)
        : $"EVENT|{NoteName(activity.Name)}|{activity.Start?.ToUniversalTime():O}|{activity.End?.ToUniversalTime():O}|{activity.RepeatWeeks}";
    private static NoteProcessing ProcessingOf(PlanningNote note)
    {
        if (note.ProcessingStateJson.Length > 0)
            return JsonSerializer.Deserialize<NoteProcessing>(note.ProcessingStateJson, NoteJson)!;
        var ids = JsonSerializer.Deserialize<Guid[]>(note.CreatedTaskIdsJson, NoteJson)!;
        var committed = ids.Length > 0 || note.PlanId is not null;
        var analysis = note.AnalysisJson.Length == 0 ? null : JsonSerializer.Deserialize<NoteAnalysis>(note.AnalysisJson, NoteJson);
        return new(committed ? note.Content : "",
            committed ? (analysis?.Tasks.Select(x => NoteName(x.Name)) ?? []).ToArray() : [],
            committed ? (analysis?.Activities?.Select(ActivityKey) ?? []).ToArray() : [], ids, committed);
    }
    private async Task<NoteProcessing> ProcessingWithTasks(PlanningNote note, CancellationToken ct)
    {
        var processing = ProcessingOf(note);
        var ids = JsonSerializer.Deserialize<Guid[]>(note.CreatedTaskIdsJson, NoteJson)!;
        // Also covers notes from older versions whose latest analysis was cleared.
        var names = await Mine<WorkTask>(note.UserId).AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => x.Name).ToArrayAsync(ct);
        return processing with { TaskNames = processing.TaskNames.Concat(names.Select(NoteName)).Distinct().ToArray() };
    }
    private static void SetProcessing(PlanningNote note, NoteProcessing processing) =>
        note.ProcessingStateJson = JsonSerializer.Serialize(processing, NoteJson);
    private static NoteActivity[] NewActivities(NoteProcessing processing, NoteActivity[] activities)
    {
        Require(activities.All(x => x is not null && x.Kind is "TASK" or "EVENT" && !string.IsNullOrWhiteSpace(x.Name)),
            "VALIDATION_ERROR", "Hoạt động không hợp lệ.");
        return activities.Where(x => !processing.ActivityKeys.Contains(ActivityKey(x)) &&
            !(x.Kind == "TASK" && processing.TaskNames.Contains(NoteName(x.Name))))
        .DistinctBy(ActivityKey).ToArray();
    }
    private static NoteAnalysis OnlyNewActivities(NoteProcessing processing, NoteAnalysis analysis) => analysis with
    {
        Tasks = analysis.Tasks.Where(x => !processing.TaskNames.Contains(NoteName(x.Name))).DistinctBy(x => NoteName(x.Name)).ToArray(),
        Activities = analysis.Activities is null ? null : NewActivities(processing, analysis.Activities),
        Messages = processing.Content.Length == 0 ? analysis.Messages : analysis.Messages.Concat([
            "Chỉ thêm việc mới từ lần sửa này. Các công việc và sự kiện đã lưu được giữ nguyên; sửa chúng trong Công việc hoặc Lịch."
        ]).ToArray()
    };
    private static NoteProcessing CommitActivities(PlanningNote note, NoteProcessing processing, NoteActivity[] activities, Guid[] taskIds) =>
        processing with { Content = note.Content, AnalysisImported = true, PendingTaskIds = taskIds,
            TaskNames = processing.TaskNames.Concat(activities.Where(x => x.Kind == "TASK").Select(x => NoteName(x.Name))).Distinct().ToArray(),
            ActivityKeys = processing.ActivityKeys.Concat(activities.Select(ActivityKey)).Distinct().ToArray() };
}
