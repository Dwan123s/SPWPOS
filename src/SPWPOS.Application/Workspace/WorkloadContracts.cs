namespace SPWPOS.Application.Workspace;

public sealed record WorkloadSession(Guid Id, Guid TaskId, string Name, DateTimeOffset Start, DateTimeOffset End,
    string Status, long? ActualSeconds, bool NeedsOutcome, bool TeamTask, long TimerSeconds, string? Outcome);
public sealed record WorkloadDay(DateOnly Date, int? WorkingMinutes, int? ReservedMinutes, int? SchedulableMinutes,
    long PlannedSeconds, long? RecordedActualSeconds, string Status, string? Advice, WorkloadSession[] Sessions);
public sealed record WorkloadTask(Guid Id, string Name, int Estimate, int RemainingMinutes, int UnscheduledMinutes,
    int? ShortfallMinutes, DateTimeOffset Deadline, bool Overdue, string Status, string? TeamStatus, string? Reason, string DeadlineAssessment, int ChecklistDone, int ChecklistTotal);
public sealed record WorkloadAction(string Kind, string Reason, Guid? TaskId = null, Guid? SessionId = null);
public sealed record WorkloadView(DateTimeOffset AsOf, DateTimeOffset RefreshAt, Guid Revision, string TimeZoneId,
    DateOnly Start, DateOnly End, WorkloadDay[] Days, WorkloadTask[] Tasks, int CompletedTasks, WorkloadAction Next,
    int TasksMissingPlanningDetails = 0, int TasksAwaitingSchedule = 0);

public sealed record TeamWorkloadMember(Guid UserId, string Name);
public sealed record TeamWorkloadTask(Guid Id, string Name, Guid Version, string Status, Guid? AssigneeId,
    Guid? DesignatedUserId, DateTimeOffset Deadline, int EstimatedMinutes, int Progress, string Risk);
public sealed record TeamWorkloadView(DateTimeOffset AsOf, DateTimeOffset RefreshAt, DateOnly Start, DateOnly End,
    TeamWorkloadMember[] Members, TeamWorkloadTask[] Tasks);
