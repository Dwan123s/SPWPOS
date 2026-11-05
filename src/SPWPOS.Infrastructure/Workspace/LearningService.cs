using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    // Called inside the optimistic workspace transaction of the mutation.
    private async Task UpdateLearningAsync(Guid uid, CancellationToken ct)
    {
        var tasks = await Mine<WorkTask>(uid).Where(x => !x.IsDeleted && x.Status == WorkTaskStatus.Completed).ToArrayAsync(ct);
        var sessions = await Mine<WorkSession>(uid).Where(x => x.Status == WorkSessionStatus.Completed).ToArrayAsync(ct);
        var all = await Mine<WorkTask>(uid).ToArrayAsync(ct);
        var observations = await Mine<EstimationObservation>(uid).ToListAsync(ct);
        foreach (var task in tasks.Where(x => !all.Any(c => c.ParentId == x.Id && !c.IsDeleted)))
        {
            var actual = sessions.Where(x => x.TaskId == task.Id).Sum(x => x.ActualSeconds) / 60d;
            var observation = observations.SingleOrDefault(x => x.TaskId == task.Id);
            if (actual <= 0) { if (observation is not null) observation.ActualMinutes = 0; continue; }
            if (observation is null)
            {
                observation = new() { UserId = uid, TaskId = task.Id, TaskType = task.TaskType, EstimatedMinutes = task.EstimatedMinutes, ObservedAt = task.CompletedAt ?? Now };
                db.EstimationObservations.Add(observation); observations.Add(observation);
            }
            observation.ActualMinutes = actual; // Corrected actual duration keeps the original estimate snapshot.
        }
        await db.SaveChangesAsync(ct);
        var state = await State(uid, ct); var biases = engine.Biases(state); var capacity = engine.Capacity(state, Now);
        foreach (var task in all.Where(x => !x.IsDeleted && x.Status is not WorkTaskStatus.Completed and not WorkTaskStatus.Cancelled))
        {
            task.AdjustedMinutes = engine.Adjusted(task, biases);
            task.AdjustmentFactor = (biases.FirstOrDefault(x => x.TaskType == task.TaskType && x.Personalized) ?? biases[0]).Ratio;
        }
        var profile = await db.PersonalCapacityProfiles.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (profile is null) { profile = new() { UserId = uid }; db.PersonalCapacityProfiles.Add(profile); }
        profile.GlobalEstimationBias = biases[0].Ratio; profile.CapacityFactor = capacity.Factor; profile.ObservationCount = observations.Count(x => x.ActualMinutes > 0); profile.UpdatedAt = Now;
        foreach (var bias in biases.Where(x => x.TaskType != "All"))
        {
            var record = await db.TaskTypeBiases.SingleOrDefaultAsync(x => x.UserId == uid && x.TaskType == bias.TaskType, ct);
            if (record is null) { record = new() { UserId = uid, TaskType = bias.TaskType }; db.TaskTypeBiases.Add(record); }
            var samples = observations.Where(x => x.TaskType == bias.TaskType && x.ActualMinutes > 0 && x.EstimatedMinutes > 0).OrderBy(x => x.ObservedAt).ThenBy(x => x.Id).ToArray();
            record.BiasFactor = bias.Ratio; record.SampleCount = bias.Samples;
            record.AverageErrorRatio = AdaptiveLearning.Ema(samples.Select(x => Math.Abs(x.ActualMinutes - x.EstimatedMinutes) / x.EstimatedMinutes), 0); record.UpdatedAt = Now;
        }
        var observedTypes = biases.Where(x => x.TaskType != "All").Select(x => x.TaskType).ToArray();
        foreach (var stale in await db.TaskTypeBiases.Where(x => x.UserId == uid && !observedTypes.Contains(x.TaskType)).ToArrayAsync(ct))
        { stale.BiasFactor = 1; stale.SampleCount = 0; stale.AverageErrorRatio = 0; stale.UpdatedAt = Now; }
        var zone = PlanningEngine.Zone(state.Preference.TimeZoneId);
        foreach (var group in state.Sessions.Where(x => x.End < Now && x.Status is WorkSessionStatus.Completed or WorkSessionStatus.Skipped or WorkSessionStatus.Missed)
            .GroupBy(x => Week(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.Start, zone).DateTime))))
        {
            var record = await Mine<CapacitySnapshot>(uid).SingleOrDefaultAsync(x => x.WeekStart == group.Key, ct);
            if (record is null) { record = new() { UserId = uid, WeekStart = group.Key }; db.CapacitySnapshots.Add(record); }
            record.AvailableMinutes = (int)group.Sum(x => (x.End - x.Start).TotalMinutes);
            record.CompletedPlannedMinutes = (int)group.Where(x => x.Status == WorkSessionStatus.Completed).Sum(x => (x.End - x.Start).TotalMinutes);
            record.CapacityFactor = engine.Capacity(state with { Sessions = group.ToArray() }, Now).Factor;
        }
    }
    private static DateOnly Week(DateOnly day) => day.AddDays(-((int)day.DayOfWeek + 6) % 7);
    private async Task<IntelligenceView> PersistRiskAsync(Guid uid, PlanInput input, CancellationToken ct)
    {
        var analysis = engine.Analyze(await State(uid, ct), input, Now);
        foreach (var previous in await Mine<RiskAssessment>(uid).Where(x => x.IsCurrent).ToArrayAsync(ct)) previous.IsCurrent = false;
        // Release filtered unique keys before inserting the new current assessment.
        await db.SaveChangesAsync(ct);
        void Record(Guid? taskId, string scope, string level, double score, double? load, string[] reasons)
        {
            var assessment = new RiskAssessment { UserId = uid, TaskId = taskId, Scope = scope, Level = level.ToUpperInvariant(), Score = score, LoadRatio = load, ComputedAt = Now };
            db.RiskAssessments.Add(assessment);
            db.RiskReasons.AddRange(reasons.Select(code => new RiskReason { UserId = uid, AssessmentId = assessment.Id, Code = code,
                DetailJson = JsonSerializer.Serialize(new { input.Start, input.HorizonDays, analysis.EffectiveCapacityMinutes, analysis.RemainingMinutes }) }));
        }
        Record(null, "OVERLOAD", analysis.RiskLevel, Math.Clamp(analysis.LoadRatio * 100, 0, 100), analysis.LoadRatio, analysis.Reasons);
        foreach (var task in analysis.Tasks) Record(task.TaskId, "TASK", task.Level, task.Score, null, task.Reasons);
        return analysis;
    }
    public Task SetNotificationSettingsAsync(Guid uid, NotificationSettingInput input, CancellationToken ct) => Mutate(uid, async () =>
    {
        Require(input.DeadlineLeadHours is >= 1 and <= 168 && input.SessionReminderLeadMinutes is >= 1 and <= 120 &&
            input.WorkloadWarningLevel is "MODERATE" or "HIGH" or "OVERLOAD", "VALIDATION_ERROR", "Cài đặt thông báo không hợp lệ.");
        var record = await db.NotificationSettings.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (record is null) { record = new() { UserId = uid }; db.NotificationSettings.Add(record); }
        record.InAppEnabled = input.InAppEnabled; record.DeadlineLeadHours = input.DeadlineLeadHours;
        record.EmailEnabled = input.EmailEnabled;
        record.SessionReminderLeadMinutes = input.SessionReminderLeadMinutes; record.WorkloadWarningLevel = input.WorkloadWarningLevel;
        await RefreshNoraReminderChannelsAsync(uid,ct);
    }, ct, false);
}
