using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record SkinView(string Code, string Name, long RequiredXp, bool Unlocked);
public sealed record GamificationProfile(long ExperiencePoints, int UserLevel, long NextLevelXp, string SelectedSkin, SkinView[] Inventory);
public sealed record SkinInput([Required, StringLength(40)] string Code);
public sealed record CatchUpInput([Required, RegularExpression("^(COMPLETED|PARTIAL|MISSED)$")] string Outcome,
    [Range(0, 100)] int? Progress = null, [Range(0, 10080)] int? ActualMinutes = null);
public sealed record CatchUpResult(Guid SessionId, string Status, Guid? ReplacementSessionId, string Message);
public sealed record AdminMetrics(int Users, int ActiveUsersToday, int CompletedSessions, long? InputTokens, long? OutputTokens,
    long FailedJobs, long ProcessMemoryBytes, double ProcessCpuMilliseconds, DateTimeOffset AsOf);
public sealed record DependencyHealth(bool Configured, bool? Reachable, double? LastObservedLatencyMilliseconds, string Detail);
public sealed record AdminHealth(bool DatabaseHealthy, DependencyHealth Email, DependencyHealth Ai, DateTimeOffset AsOf);
