using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport.NoraSandbox;
namespace SPWPOS.UnitTests;

public sealed class NoraSandboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-26T09:00:00+07:00");
    private static SandboxSnapshot State() => new(new() { TimeZoneId = "Asia/Ho_Chi_Minh", MinimumSessionMinutes = 15,
        MaximumSessionMinutes = 120, MaximumDailyMinutes = 180, BreakMinutes = 0 }, [], [], [],
        [new() { DayOfWeek = 1, StartMinute = 1140, EndMinute = 1320 }], [], [], ["OTHER"], []);
    private static AgentTurnView Result(string status = "APPLIED") => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Yêu cầu mẫu", "Kết quả mẫu", status, "", null, [], [], Now);

    [Theory] [InlineData("overlap")] [InlineData("deadline")] [InlineData("outside-free-time")] [InlineData("duration")] [InlineData("work-window")]
    public void Exported_schedule_violations_are_detected_independently_of_the_planning_engine(string defect)
    {
        var before = State();
        var task = new WorkTask { Name = "Bài tập", TaskType = "OTHER", EstimatedMinutes = 60,
            Deadline = Now.AddHours(11), IsHardDeadline = true, MinimumSessionMinutes = 15 };
        var start = Now.AddHours(defect == "outside-free-time" ? 8 : 10);
        var session = new WorkSession { TaskId = task.Id, Start = start, End = start.AddMinutes(defect == "duration" ? 90 : 60) };
        if (defect == "deadline") task.Deadline = start.AddMinutes(30);
        if (defect == "duration") task.Deadline = Now.AddHours(13);
        if (defect == "work-window") { task.Deadline = Now.AddHours(13); task.WorkWindowStart = Now.AddHours(11); task.WorkWindowEnd = Now.AddHours(12); }
        var after = before with { Tasks = [task], Sessions = [session], Events = defect == "overlap"
            ? [new() { Name = "Họp", Start = start, End = start.AddMinutes(30) }] : [] };

        var checks = SandboxOracle.Evaluate(new(), Result(), null, before, after, Now);

        Assert.Contains(checks, x => !x.Passed && x.Critical);
    }

    [Fact] public void Exclusive_date_availability_replaces_weekly_and_additive_free_hours_in_the_oracle()
    {
        var state = State() with { Exceptions = [
            new() { Date = new(2026, 10, 26), StartMinute = 900, EndMinute = 960, IsAvailable = true },
            new() { Date = new(2026, 10, 26), StartMinute = 1200, EndMinute = 1260, IsAvailable = true, OverridesWeeklyAvailability = true },
            new() { Date = new(2026, 10, 26), StartMinute = 1215, EndMinute = 1230, IsAvailable = false }] };
        var minutes = SandboxOracle.FreeMinutes(state, new(2026, 10, 26));
        Assert.False(minutes[900]); Assert.False(minutes[1140]); Assert.True(minutes[1200]); Assert.False(minutes[1215]);
        Assert.True(minutes[1230]); Assert.False(minutes[1260]); Assert.Equal(45, minutes.Count(x => x));
    }

    [Fact] public void A_read_only_result_that_changed_the_database_fails_even_if_the_reply_says_nothing_changed()
    {
        var before = State(); var after = before with { Events = [new() { Name = "Unexpected", Start = Now.AddHours(10), End = Now.AddHours(11) }] };
        var checks = SandboxOracle.Evaluate(new() { Statuses = ["ANSWERED"], Unchanged = true }, Result("ANSWERED"), null, before, after, Now);
        Assert.Contains(checks, x => !x.Passed && x.Critical);
    }

    [Fact] public void Snapshot_diff_contains_real_before_and_after_values_for_edits_and_additions()
    {
        var before = State(); var meeting = new FixedEvent { Name = "Họp", Start = Now.AddHours(9), End = Now.AddHours(10) };
        before = before with { Events = [meeting] };
        var edited = new FixedEvent { Id = meeting.Id, Name = "Họp", Start = Now.AddHours(10), End = Now.AddHours(11) };
        var after = before with { Events = [edited], Tasks = [new() { Name = "Viết", EstimatedMinutes = 45 }] };

        var changes = SandboxOracle.Diff(before, after);

        var update = Assert.Single(changes, x => x.Entity == "event");
        Assert.Equal("UPDATE", update.Operation);
        Assert.Equal(Now.AddHours(9), update.Before!.Value.GetProperty("start").GetDateTimeOffset());
        Assert.Equal(Now.AddHours(10), update.After!.Value.GetProperty("start").GetDateTimeOffset());
        Assert.Equal("ADD", Assert.Single(changes, x => x.Entity == "task").Operation);
    }

    [Fact] public void A_provider_failure_cannot_be_counted_as_a_passing_clarification()
    {
        var state = State();
        var checks = SandboxOracle.Evaluate(new() { Statuses = ["NEEDS_INPUT"], Unchanged = true }, null,
            "AGENT_AI_QUOTA_EXCEEDED", state, state, Now);
        Assert.Contains(checks, x => x.Name == "expected-outcome" && !x.Passed);
    }

    [Fact] public void An_uninterpretable_provider_response_cannot_pass_an_expected_safe_rejection()
    {
        var state = State();
        var checks = SandboxOracle.Evaluate(new() { Statuses = ["BLOCKED", "NEEDS_INPUT", "REJECTED"], Unchanged = true },
            null, "AGENT_AI_INVALID_RESPONSE", state, state, Now);
        Assert.Contains(checks, x => x.Name == "expected-outcome" && !x.Passed);
    }

    [Fact] public void A_valid_schedule_passes_without_requiring_a_specific_model_explanation()
    {
        var before = State(); var task = new WorkTask { Name = "Ôn SQL", TaskType = "OTHER", EstimatedMinutes = 60,
            Deadline = Now.AddHours(13), IsHardDeadline = true, MinimumSessionMinutes = 15 };
        var after = before with { Tasks = [task], Sessions = [new() { TaskId = task.Id, Start = Now.AddHours(10), End = Now.AddHours(11) }] };
        var checks = SandboxOracle.Evaluate(new() { TaskCount = 1, Tasks = [new("SQL", 60, "2026-10-26T22:00:00+07:00")] }, Result(), null, before, after, Now);
        Assert.NotEmpty(checks); Assert.All(checks, x => Assert.True(x.Passed, x.Name + ": " + x.Detail));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SPWPOS.sln"))) return directory.FullName;
        throw new InvalidOperationException("Cannot locate test repository.");
    }

    [Theory] [InlineData("WORK", true)] [InlineData("OTHER", false)]
    public void Event_category_expectations_detect_a_work_shift_saved_as_other(string category, bool valid)
    {
        var before = State(); var start = Now.AddHours(4); var end = start.AddHours(4);
        var after = before with { Events = [new() { Name = "Đi làm", Start = start, End = end, EventType = category }] };
        var checks = SandboxOracle.Evaluate(new() { Events = [new("Đi làm", start.ToString("O"), end.ToString("O"), "WORK")] }, Result(), null, before, after, Now);
        Assert.Equal(valid, checks.All(x => x.Passed));
    }

    [Fact] public async Task Offline_runner_exercises_real_service_actions_exports_evidence_and_obeys_the_call_cap()
    {
        var directory = Directory.CreateTempSubdirectory("nora-sandbox-test-").FullName;
        try
        {
            var report = await SandboxRunner.RunAsync(new(RepositoryRoot(), directory, CaseIds: ["A01", "A04", "A07", "B01", "S04", "H01", "H02", "H03", "H09", "H10"], MaxCalls: 15), null, TestContext.Current.CancellationToken);
            Assert.Equal(10, report.Cases.Length);
            Assert.All(report.Cases, x => Assert.True(x.Status == "PASS", x.Id + ": " + x.Reason + " " + string.Join(';', x.Turns.SelectMany(t => t.Checks).Where(c => !c.Passed).Select(c => c.Name + c.Detail))));
            Assert.All(report.Cases, x => Assert.Equal("NOT_EVALUATED", x.ResponseReview));
            Assert.Contains(report.Cases.SelectMany(x => x.Turns), x => x.Actions.Any(a => a.Entity == "task" && a.Operation == "ADD"));
            using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "results.json"), TestContext.Current.CancellationToken));
            Assert.Equal("Offline", json.RootElement.GetProperty("mode").GetString());
            Assert.Contains("Saved work sessions", await File.ReadAllTextAsync(Path.Combine(directory, "report.md"), TestContext.Current.CancellationToken));
            Assert.Contains("actualChanges", await File.ReadAllTextAsync(Path.Combine(directory, "turns.csv"), TestContext.Current.CancellationToken));
            Assert.Contains("verifiedOutcome", await File.ReadAllTextAsync(Path.Combine(directory, "turns.csv"), TestContext.Current.CancellationToken));
            Assert.Contains("Verified outcome", await File.ReadAllTextAsync(Path.Combine(directory, "report.md"), TestContext.Current.CancellationToken));
            var rejected = report.Cases.Single(x => x.Id == "H03").Turns[1];
            Assert.Equal("AGENT_REQUEST_REUSED", rejected.ErrorCode);
            Assert.Null(rejected.Result);
            var formulaTurn = report.Cases[0].Turns[0] with { Result = report.Cases[0].Turns[0].Result! with { Reply = "=2+2" } };
            var formulaReport = report with { Cases = [report.Cases[0] with { Turns = [formulaTurn] }] };
            await SandboxExport.WriteAsync(formulaReport, Path.Combine(directory, "formula"), TestContext.Current.CancellationToken);
            Assert.Contains("\"'=2+2\"", await File.ReadAllTextAsync(Path.Combine(directory, "formula", "turns.csv"), TestContext.Current.CancellationToken));

            var limited = await SandboxRunner.RunAsync(new(RepositoryRoot(), Path.Combine(directory, "limited"), CaseIds: ["A01", "B01"], MaxCalls: 1), null, TestContext.Current.CancellationToken);
            Assert.Equal(1, limited.CallsUsed); Assert.Equal("NOT_RUN", limited.Cases.Single(x => x.Id == "B01").Status);
            Assert.DoesNotContain(limited.Cases.Single(x => x.Id == "B01").Turns, x => x.Traces.Length > 0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
