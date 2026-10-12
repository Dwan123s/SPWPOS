using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record NoteInput([Required, StringLength(120)] string Title,
    [Required, StringLength(8000)] string Content, Guid? Version = null);
public sealed record NoteAnalysisInput(Guid Version, bool UseAi = false);
public sealed record NoteImportInput(Guid Version, [Required, MinLength(1), MaxLength(12)] TaskInput[] Tasks);
public sealed record NoteAnalysis(TaskInput[] Tasks, string Source, string[] Messages);
public sealed record NoteView(Guid Id, string Title, string Content, Guid Version,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, NoteAnalysis? Analysis, Guid[] CreatedTaskIds, Guid? PlanId = null);
public sealed record NoteScheduleInput(Guid Version, bool UseAi = false, [Range(1, 30)] int HorizonDays = 14);
public sealed record NoteScheduleResult(NoteView Note, PlanView Plan);
