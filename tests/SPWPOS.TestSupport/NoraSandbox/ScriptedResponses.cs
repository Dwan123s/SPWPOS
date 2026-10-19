using System.Globalization;
using SPWPOS.Application.Workspace;
namespace SPWPOS.TestSupport.NoraSandbox;

// Controlled interpreter fixtures exercise backend decisions, never prompt comprehension.
public static class SandboxScriptedResponses
{
    public static AgentIntent Build(SandboxCase scenario, int stepIndex, SandboxStep step, SandboxSnapshot state)
    {
        var empty = AgentAiTestDouble.Empty(); var id = scenario.Id;
        var expected = step.Expected;
        var tasks = new List<AgentTaskCommand>(); var scheduleIds = new List<Guid>();
        AgentTaskCommand New(string name, int minutes, int day = 0, int hour = 22, bool split = true, string type = "STUDY") =>
            new(null, name, SandboxEnvironment.At(day, hour), minutes, 2, type, split);
        AgentIntent Question() => empty with { Question = "Bạn xác nhận giúp mình chi tiết còn thiếu hoặc phương án thay thế nhé?" };
        if (id is "B01" or "B02" or "B03") return empty with { Tasks = [new(null, "Công việc mới", id == "B02" || id == "B03" ? null : SandboxEnvironment.At(2, 22),
            id == "B01" || id == "B03" ? null : 45, 2, "OTHER", true)] };
        if (id == "H10" || step.Operation == "MixedClarification" || id == "B09") return Question() with {
            Reply = "Đã lưu cuộc họp và công việc.", Tasks = [New("Slide", 90, 2)],
            Events = [new("Họp", SandboxEnvironment.At(1, 18), SandboxEnvironment.At(1, 19))],
            ReplaceWeeklyAvailability = true, WeeklySlots = [new(1, 1140, 1320)], Reschedule = true
        };
        if (id is "D02" or "D04" or "F01" || id == "S06" && stepIndex == 0) return empty with { Tasks = [New("Bài thi", 90, split: false)] };
        if (id == "D05") return empty with { Tasks = [New("Ôn từ vựng", 60)] };
        if (id == "D06") return empty with { Tasks = [New("Đọc sách", 120, hour: 21)] };
        if (id == "D08") return empty with { ScheduleTaskIds = [state.Tasks.Single(x => x.Name == "Viết bài").Id] };
        if (id == "D09") return empty with { Tasks = [New("SQL", 30, 1)], Events = [new("Họp", SandboxEnvironment.At(0, 19), SandboxEnvironment.At(0, 20))] };
        if (id == "D10") return empty with { Events = [new("Họp", SandboxEnvironment.At(0, 19, 30), SandboxEnvironment.At(0, 20, 30))] };
        if (id == "D12") return empty with { Events = [new("Họp tuần", SandboxEnvironment.At(0, 18), SandboxEnvironment.At(0, 19), RepeatWeeks: 3)] };
        if (id == "D13") return empty with { Tasks = [New("A", 90, hour: 21), New("B", 60, hour: 21)] };
        if (id == "F03") return empty with { ScheduleTaskIds = [Guid.Parse(step.Prompt.Split("ID ")[1].Split(' ')[0])] };
        if (id == "F07") return empty with { Tasks = [New("Thiết kế poster", 60, 2, type: "DESIGN")] };
        if (id == "G03") return empty with { Events = [new("Họp", SandboxEnvironment.At(0, 8), SandboxEnvironment.At(0, 8, 30))] };
        if (id == "G04") return empty with { Events = [new("Họp", SandboxEnvironment.At(31, 19), SandboxEnvironment.At(31, 20))] };
        if (id == "E04")
        {
            var meeting = state.Events.Single(x => x.Name == "Họp nhóm");
            return empty with { Events = [new(meeting.Name, meeting.Start, meeting.End, meeting.Description, meeting.EventType, ExistingId: meeting.Id)] };
        }
        if (id == "E09") return empty with { ReplaceWeeklyAvailability = true, WeeklySlots = state.Weekly.Select(x => new AvailabilityInput(x.DayOfWeek, x.StartMinute, x.EndMinute)).ToArray() };
        if (id.StartsWith('H') && step.Operation is "Send" or "SendPreview") return empty with { Tasks = [New(id == "H09" ? "Viết báo cáo" : "Ôn SQL", 60, 1, type: id == "H09" ? "WRITING" : "STUDY")] };
        if (expected.Statuses.Contains("NEEDS_INPUT") && expected.Unchanged && !expected.Statuses.Contains("ANSWERED")) return Question();
        if (expected.Unchanged && expected.Statuses.Contains("ANSWERED")) return empty;
        if (id == "C03") return empty with { Exceptions = [new(new(2026, 10, 31), 540, 660, true)] };
        if (id == "C04") return empty with { Exceptions = [new(new(2026, 10, 27), 1200, 1260, false)] };
        if (id == "C05") return empty with { Exceptions = [new(new(2026, 10, 30), 0, 1200, false), new(new(2026, 10, 30), 1260, 1440, false)] };
        if (id == "C09") return empty with { Exceptions = [new(new(2026, 10, 27), 1260, 1320, false, "Bận học", state.Exceptions.Single().Id)] };
        if (expected.Weekly is { } weekly) return empty with { ReplaceWeeklyAvailability = true, WeeklySlots = weekly.Select(x => new AvailabilityInput(x[0], x[1], x[2])).ToArray() };
        if (id == "S01" && stepIndex is 2 or 3)
        {
            var original = state.Events.SingleOrDefault(x => SandboxOracle.Fold(x.Name).Contains("hop nhom", StringComparison.Ordinal));
            return empty with { Events = [new("Họp nhóm", SandboxEnvironment.At(0, stepIndex == 2 ? 19 : 20),
                stepIndex == 2 ? SandboxEnvironment.At(0, 20, 30) : SandboxEnvironment.At(0, 21), EventType: "MEETING", ExistingId: original?.Id)] };
        }
        foreach (var target in expected.Tasks)
        {
            var existing = state.Tasks.SingleOrDefault(x => target.Id is not null ? x.Id.ToString() == target.Id : SandboxOracle.Fold(x.Name).Contains(SandboxOracle.Fold(target.Match), StringComparison.Ordinal));
            if (id == "S01" && stepIndex == 1 && existing is not null) continue;
            var deadline = DateTimeOffset.Parse(target.Deadline, CultureInfo.InvariantCulture);
            if (existing is not null && existing.EstimatedMinutes == target.Minutes && existing.Deadline == deadline &&
                (target.Priority is null || target.Priority == existing.Priority) && (target.Splittable is null || target.Splittable == existing.Splittable)) scheduleIds.Add(existing.Id);
            else tasks.Add(new(existing?.Id, existing?.Name ?? target.Match, deadline, target.Minutes, target.Priority, existing is null ? "STUDY" : "", target.Splittable ?? true,
                target.WorkWindowStart is { } from ? DateTimeOffset.Parse(from, CultureInfo.InvariantCulture) : null,
                target.WorkWindowEnd is { } to ? DateTimeOffset.Parse(to, CultureInfo.InvariantCulture) : null));
        }
        var meetings = expected.Events.Select(target => {
            var start = DateTimeOffset.Parse(target.Start, CultureInfo.InvariantCulture); var end = DateTimeOffset.Parse(target.End, CultureInfo.InvariantCulture);
            var existing = state.Events.SingleOrDefault(x => SandboxOracle.Fold(x.Name).Contains(SandboxOracle.Fold(target.Match), StringComparison.Ordinal) && x.Start.Date == start.Date);
            return new AgentEventCommand(existing?.Name ?? target.Match, start, end, EventType: existing?.EventType ?? "OTHER", ExistingId: existing?.Id);
        }).ToArray();
        if (id == "D11") meetings = [new("Cuộc hẹn mới", SandboxEnvironment.At(0, 19), SandboxEnvironment.At(0, 20))];
        return empty with { Tasks = tasks.ToArray(), ScheduleTaskIds = scheduleIds.ToArray(), Events = meetings };
    }
}
