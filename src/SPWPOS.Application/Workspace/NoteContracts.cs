using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record NoteInput([Required, StringLength(120)] string Title,
    [Required, StringLength(8000)] string Content, Guid? Version = null);
// UseAi is retained for older clients; note analysis always uses AI.
public sealed record NoteAnalysisInput(Guid Version, bool UseAi = true);
public sealed record NoteImportInput(Guid Version, [Required, MinLength(1), MaxLength(12)] TaskInput[] Tasks);
public sealed record NoteAnalysis(TaskInput[] Tasks, string Source, string[] Messages, bool NeedsReview = false,
    NoteActivity[]? Activities = null);
public sealed record NoteActivity(string Kind, string Name, string Description = "", string Type = "OTHER",
    DateTimeOffset? Start = null, DateTimeOffset? End = null, int? EstimatedMinutes = null,
    DateTimeOffset? Deadline = null, int Priority = 2, bool IsHardDeadline = false,
    int RepeatWeeks = 1, string Question = "");
public sealed record NoteActivityPlanInput(Guid Version, [Required, MinLength(1), MaxLength(12)] NoteActivity[] Activities,
    [Range(1, 30)] int HorizonDays = 14, bool ReviewedConditions = false);
public sealed record NotePlanContext(Guid NoteId, Guid NoteVersion, NoteActivity[] Activities, Guid[] TaskIds, EventView[] Events);
public sealed record NoteApplyInput(Guid Version);
public sealed record NoteAgentCapabilities(bool AiAvailable, int MaxTasks = 12, string Provider = "OpenAI");
public sealed record NoteView(Guid Id, string Title, string Content, Guid Version,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, NoteAnalysis? Analysis, Guid[] CreatedTaskIds, Guid? PlanId = null);
public sealed record NoteScheduleInput(Guid Version, bool UseAi = true, [Range(1, 30)] int HorizonDays = 14);
public sealed record NoteAgentStep(string Code, string Message);
public sealed record NoteScheduleResult(NoteView Note, PlanView Plan, NoteAgentStep[]? Steps = null);
