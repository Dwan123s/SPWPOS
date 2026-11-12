namespace SPWPOS.Domain.Entities;

public sealed record RoutineDay(DateOnly Date, int AwakeStart, int AwakeEnd, int LunchStart, int LunchEnd,
    int DinnerStart, int DinnerEnd, int RestStart = 0, int RestEnd = 0, int BreakfastStart = 0, int BreakfastEnd = 0,
    bool UseTimetableHours = false);

public sealed class AvailabilityPolicy : OwnedRecord
{
    public int AwakeStart { get; set; } = 420;
    public int AwakeEnd { get; set; } = 1350;
    public int LunchStart { get; set; } = 705;
    public int LunchEnd { get; set; } = 765;
    public int DinnerStart { get; set; } = 1080;
    public int DinnerEnd { get; set; } = 1140;
    public int CommuteMinutes { get; set; } = 30;
    public int FocusMinutes { get; set; } = 45;
    public DateTimeOffset ConfirmedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    private string routineDaysJson = "[]";
    private RoutineDay[]? routineDays;
    public string RoutineDaysJson { get => routineDaysJson; set { routineDaysJson = value; routineDays = null; } }
    public RoutineDay? ForDate(DateOnly date) => (routineDays ??= System.Text.Json.JsonSerializer.Deserialize<RoutineDay[]>(routineDaysJson) ?? []).FirstOrDefault(x => x.Date == date);
    public DateOnly[] TimetableWeeks() => (routineDays ??= System.Text.Json.JsonSerializer.Deserialize<RoutineDay[]>(routineDaysJson) ?? [])
        .Where(day => day.UseTimetableHours)
        .Select(day => day.Date.AddDays(-(((int)day.Date.DayOfWeek + 6) % 7))).Distinct().Order().ToArray();
    public bool AppliesOn(DateOnly date) => (routineDays ??= System.Text.Json.JsonSerializer.Deserialize<RoutineDay[]>(routineDaysJson) ?? []).Length == 0 || routineDays.Any(x => x.Date == date);
}
public sealed class PrivatePlanningProposal : OwnedRecord
{
    public string Kind { get; set; } = "POLICY";
    public string PayloadJson { get; set; } = "{}";
    public Guid Revision { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Applied { get; set; }
}
public sealed class TeamProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid CreatorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class TeamMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public bool Active { get; set; } = true;
    public string ApprovalStatus { get; set; } = "Approved";
    public Guid? JoinInviteId { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public Guid MutationVersion { get; set; } = Guid.NewGuid();
}
public sealed class TeamInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public int RemainingUses { get; set; } = 20;
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class TeamTask : ITaskImportance
{
    public string ImportanceLevel { get; set; } = "Unknown";
    public string ImportanceSource { get; set; } = "Unknown";
    public string ImportanceReason { get; set; } = "";
    public double? ImportanceConfidence { get; set; }
    public DateTimeOffset? ImportanceConfirmedAt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? DesignatedUserId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int EstimatedMinutes { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public int Priority { get; set; } = 2;
    public bool Splittable { get; set; } = true;
    public Guid? AssigneeId { get; set; }
    public int Progress { get; set; }
    public string Status { get; set; } = "Open";
    public Guid Version { get; set; } = Guid.NewGuid();
    public string? PendingChangeJson { get; set; }
    public Guid? CompletionRequestId { get; set; }
    public DateTimeOffset? CompletionSubmittedAt { get; set; }
    public DateTimeOffset? CompletionReviewedAt { get; set; }
    public Guid? CompletionReviewedBy { get; set; }
    public bool? CompletionApproved { get; set; }
    public string? CompletionReviewNote { get; set; }
    public int ProgressBeforeReview { get; set; }
}
public sealed class TeamTaskDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid UploadedBy { get; set; }
    public string Name { get; set; } = "";
    public string MimeType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public int Size { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class TeamTaskAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamTaskId { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkTaskId { get; set; }
    public Guid ProposalId { get; set; }
    public bool Active { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class TeamCompletionSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamTaskId { get; set; }
    public Guid UserId { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class TeamCompletionFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public string Name { get; set; } = "";
    public string MimeType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public int Size { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class TeamClaimProposal : OwnedRecord
{
    public Guid TeamTaskId { get; set; }
    public Guid TeamTaskVersion { get; set; }
    public Guid MembershipVersion { get; set; }
    public Guid Revision { get; set; }
    public Guid? AppliedRevision { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Proposed";
}
public sealed class TeamActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? TaskId { get; set; }
    public string Kind { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class TeamChatMessage
{
    public long Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public string Text { get; set; } = "";
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
