using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private sealed record NoraReminderDraft(string Operation, Guid Id, string Message, DateTimeOffset At);
    private sealed record NoraReminderUndo(Guid Id, bool Visible, string Message, DateTimeOffset At, DateTimeOffset? ReadAt);

    public Task<NoraReminderView[]> GetNoraRemindersAsync(Guid uid, CancellationToken ct) => Mutate(uid, async () =>
    {
        await MigrateInboxRemindersAsync(uid, ct);
        return await ReadNoraRemindersAsync(uid, ct);
    }, ct, false);

    private async Task MigrateInboxRemindersAsync(Guid uid, CancellationToken ct)
    {
        // Existing alarms keep their IDs/deadlines/read state, independently of
        // the Inbox note. No new Inbox note can create or cancel an alarm.
        foreach (var row in await Mine<InAppNotification>(uid).Where(x => x.Kind == "INBOX_REMINDER").ToArrayAsync(ct))
        { row.Kind = "NORA_REMINDER"; row.Title = "NORA nhắc bạn"; }
        await db.SaveChangesAsync(ct);
    }

    private async Task<NoraReminderView[]> ReadNoraRemindersAsync(Guid uid, CancellationToken ct) =>
        (await Mine<InAppNotification>(uid).Where(x => x.Kind == "NORA_REMINDER" && x.ReadAt == null)
            .OrderBy(x => x.ScheduledFor).Take(100).ToArrayAsync(ct))
        .Select(x => new NoraReminderView(x.Id, x.Message, x.ScheduledFor, x.ScheduledFor <= Now)).ToArray();

    public Task CancelNoraReminderAsync(Guid uid, Guid id, CancellationToken ct) => Mutate(uid, async () =>
    {
        var row = await Find<InAppNotification>(uid, id, ct);
        Require(row.Kind is "NORA_REMINDER" or "INBOX_REMINDER", "NOT_FOUND", "Không tìm thấy lời nhắc của bạn.", 404);
        row.Kind = "NORA_REMINDER"; row.IsVisible = false; row.ReadAt=Now; row.EmailPending = false; row.TelegramPending = false;
    }, ct, false);

    private DateTimeOffset ReminderTime(AgentTurn turn, NoraReminderCommand command)
    {
        var source = MatchText(turn.Text);
        var match = Regex.Match(source,
            @"(?:(?:sau|trong)\s+(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>giay|phut|tieng|gio|ngay)\b|(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>giay|phut|tieng|gio|ngay)\s+nua\b)",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (match.Success)
        {
            Require(decimal.TryParse(match.Groups["amount"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount), "INVALID_REMINDER_TIME", "Kiểm tra lại thời gian nhắc.");
            var multiplier = match.Groups["unit"].Value switch { "giay" => 1, "phut" => 60, "gio" or "tieng" => 3600, _ => 86400 };
            Require(amount >= 1m / multiplier && amount <= 2592000m / multiplier,
                "INVALID_REMINDER_TIME", "Bạn chọn thời gian nhắc từ 1 giây đến 30 ngày nhé.");
            return turn.CreatedAt.AddSeconds((double)(amount * multiplier));
        }
        Require(command.At is { } at && at > Now && at <= Now.AddDays(30),
            "INVALID_REMINDER_TIME", "Bạn chọn giờ nhắc sắp tới, trong vòng 30 ngày nhé.");
        return command.At!.Value;
    }

    private async Task<AgentTurnView> PrepareNoraReminderAsync(Guid uid, AgentTurn turn, AgentIntent intent,
        WorkPreference preference, NoraRunView? run, CancellationToken ct)
    {
        var command = intent.Reminder!;
        Require(command.Operation is "Create" or "Cancel" or "List", "INVALID_REMINDER", "Thao tác lời nhắc không hợp lệ.");
        Require(!intent.ReplaceWeeklyAvailability && intent.WeeklySlots.Length == 0 && intent.Exceptions.Length == 0 &&
            intent.Events.Length == 0 && intent.Tasks.Length == 0 && intent.ScheduleTaskIds.Length == 0 && !intent.Reschedule,
            "REMINDER_MIXED_ACTIONS", "Bạn gửi riêng yêu cầu đặt nhắc và yêu cầu thay đổi lịch nhé.");
        await MigrateInboxRemindersAsync(uid, ct);
        if (command.Operation == "List")
        {
            var rows = await ReadNoraRemindersAsync(uid, ct);
            return SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text,
                ReminderListReply(rows, preference), "ANSWERED", "", null, [], [], turn.CreatedAt, Run: run));
        }
        if (intent.Question.Length > 0)
            return SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text, intent.Reply,
                "NEEDS_INPUT", intent.Question, null, [], [], turn.CreatedAt, Options: intent.Options, Run: run));
        NoraReminderDraft draft;
        if (command.Operation == "Create")
        {
            var message = command.Message.Trim();
            if(message.Length == 0 || command.At is null && !Regex.IsMatch(MatchText(turn.Text),@"\d+(?:[.,]\d+)?\s*(?:giay|phut|tieng|gio|ngay)\b",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
                return SaveTurn(turn,new(turn.Id,turn.ConversationId,turn.RequestId,turn.Text,
                    "Mình cần thêm một chi tiết để đặt lời nhắc.","NEEDS_INPUT",message.Length == 0 ? "Bạn muốn mình nhắc việc gì?" : "Bạn muốn mình nhắc vào lúc nào?",null,[],[],turn.CreatedAt,Run:run));
            Require(message.Length is > 0 and <= 500, "INVALID_REMINDER", "Bạn cho mình biết nội dung cần nhắc, tối đa 500 ký tự nhé.");
            Require(await Mine<InAppNotification>(uid).CountAsync(x => x.Kind == "NORA_REMINDER" && x.ReadAt == null && x.ScheduledFor > Now, ct) < 100,
                "REMINDER_LIMIT", "Bạn đang có 100 lời nhắc sắp tới. Hủy lời nhắc không cần dùng trước nhé.");
            draft = new("Create", Guid.NewGuid(), message, ReminderTime(turn, command));
        }
        else
        {
            if (command.ReminderId is null)
            {
                var query = MatchText(command.Message);
                var matches = (await ReadNoraRemindersAsync(uid, ct)).Where(x => query.Length > 0 && MatchText(x.Message).Contains(query, StringComparison.Ordinal)).ToArray();
                if(matches.Length != 1)
                    return SaveTurn(turn,new(turn.Id,turn.ConversationId,turn.RequestId,turn.Text,
                        matches.Length == 0 ? "Mình chưa tìm thấy lời nhắc khớp nội dung đó." : "Có nhiều lời nhắc trùng nội dung.",
                        "NEEDS_INPUT", "Bạn muốn hủy lời nhắc nào? Chọn trong mục Lời nhắc hoặc gửi nội dung cụ thể nhé.",null,[],[],turn.CreatedAt,Run:run));
                command=command with {ReminderId=matches[0].Id};
            }
            var row = await Find<InAppNotification>(uid, command.ReminderId!.Value, ct);
            Require(row.Kind == "NORA_REMINDER" && row.ReadAt == null, "NOT_FOUND", "Không tìm thấy lời nhắc đang hoạt động của bạn.", 404);
            draft = new("Cancel", row.Id, row.Message, row.ScheduledFor);
        }
        var local = TimeZoneInfo.ConvertTime(draft.At, PlanningEngine.Zone(preference.TimeZoneId));
        turn.CommandJson = JsonSerializer.Serialize(intent, AgentJson);
        turn.ChangesJson = JsonSerializer.Serialize(draft, AgentJson);
        turn.ExpiresAt = Now.AddMinutes(30);
        turn.ExpectedRevision = preference.Revision;
        return SaveTurn(turn, new(turn.Id, turn.ConversationId, turn.RequestId, turn.Text,
            draft.Operation == "Create" ? $"Lời nhắc “{draft.Message}” vào {local:HH:mm dd/MM/yyyy}."
                : $"Hủy lời nhắc “{draft.Message}” ({local:HH:mm dd/MM/yyyy}).",
            "PREVIEW", "", null, [new("REMINDER", draft.Message, "", $"{local:HH:mm dd/MM/yyyy}")], [], turn.CreatedAt,
            Run: run, Outcome: "Chưa thay đổi lời nhắc. Bạn có thể áp dụng bản xem trước này."));
    }

    private static bool IsNoraReminder(AgentTurn turn) => turn.CommandJson.Length > 0 &&
        JsonSerializer.Deserialize<AgentIntent>(turn.CommandJson, AgentJson)?.Reminder is not null;

    private async Task<AgentTurnView> ApplyNoraReminderAsync(Guid uid, AgentTurn turn, CancellationToken ct)
    {
        var draft = JsonSerializer.Deserialize<NoraReminderDraft>(turn.ChangesJson, AgentJson)!;
        var appliedView=TurnView(turn);
        if(appliedView.Run is { } runView)
        {
            var run=await Mine<AgentRun>(uid).SingleOrDefaultAsync(x=>x.Id==runView.Id,ct);
            if(run is not null) {run.Status="APPLIED";run.UpdatedAt=Now;run.Version=Guid.NewGuid();}
            appliedView=appliedView with {Run=runView with {Status="APPLIED"}};
        }
        var row = await Mine<InAppNotification>(uid).SingleOrDefaultAsync(x => x.Id == draft.Id, ct);
        var original = row?.IsVisible ?? false;
        var originalReadAt=row?.ReadAt;
        if (draft.Operation == "Create")
        {
            Require(draft.At > Now || turn.CreatedAt >= Now.AddMinutes(-2), "AGENT_PREVIEW_EXPIRED", "Giờ nhắc đã qua. Bạn đặt một lời nhắc mới nhé.", 409);
            Require(row is null, "REMINDER_STALE", "Lời nhắc đã thay đổi. Tải lại để kiểm tra.", 409);
            row = new() {Id=draft.Id, UserId=uid, Kind="NORA_REMINDER", Title="NORA nhắc bạn", Message=draft.Message,
                ScheduledFor=draft.At, CreatedAt=draft.At, DedupeKey=$"nora-reminder:{turn.Id}"};
            db.InAppNotifications.Add(row);
            await ConfigureNoraReminderChannelsAsync(uid,row,ct);
        }
        else
        {
            Require(row is not null && row.Kind == "NORA_REMINDER" && row.Message == draft.Message && row.ScheduledFor == draft.At,
                "REMINDER_STALE", "Lời nhắc đã thay đổi. Tải lại để kiểm tra.", 409);
            row!.IsVisible=false; row.ReadAt=Now; row.EmailPending=false; row.TelegramPending=false;
        }
        turn.UndoJson = JsonSerializer.Serialize(new NoraReminderUndo(row.Id, original, row.Message, row.ScheduledFor, originalReadAt), AgentJson);
        var preference = await Preference(uid, ct);
        turn.AppliedRevision = preference.Revision;
        var local = TimeZoneInfo.ConvertTime(draft.At, PlanningEngine.Zone(preference.TimeZoneId));
        var view = appliedView;
        return SaveTurn(turn, view with {Status="APPLIED", CanUndo=true,
            Reply=draft.Operation == "Create" ? $"Được, mình đã đặt nhắc bạn “{draft.Message}” vào {local:HH:mm dd/MM/yyyy}."
                : $"Mình đã hủy lời nhắc “{draft.Message}”.", Outcome="Bạn có thể quản lý lời nhắc trong mục Lời nhắc của NORA."});
    }

    private async Task<AgentTurnView> UndoNoraReminderAsync(Guid uid, AgentTurn turn, CancellationToken ct)
    {
        var undo=JsonSerializer.Deserialize<NoraReminderUndo>(turn.UndoJson,AgentJson)!;
        var row=await Mine<InAppNotification>(uid).SingleOrDefaultAsync(x=>x.Id==undo.Id,ct);
        var draft=JsonSerializer.Deserialize<NoraReminderDraft>(turn.ChangesJson,AgentJson)!;
        Require(row is not null && row.Kind=="NORA_REMINDER" && row.Message==undo.Message && row.ScheduledFor==undo.At &&
            (row.ReadAt is null) == (draft.Operation == "Create"),
            "AGENT_UNDO_STALE","Lời nhắc đã thay đổi, không thể hoàn tác bản cũ.",409);
        row!.IsVisible=undo.Visible; row.ReadAt=draft.Operation == "Create" ? Now : undo.ReadAt;
        row.EmailPending=false; row.TelegramPending=false;
        if(row.ReadAt is null) await ConfigureNoraReminderChannelsAsync(uid,row,ct);
        return SaveTurn(turn,TurnView(turn) with {Status="UNDONE",CanUndo=false,Reply="Mình đã hoàn tác thay đổi lời nhắc.",Outcome=""});
    }

    private static string ReminderListReply(NoraReminderView[] rows, WorkPreference preference) => rows.Length == 0
        ? "Bạn chưa có lời nhắc. Thử nhắn: “1 phút nữa nhắc tôi trả lời email khách hàng”."
        : "Lời nhắc của bạn:\n" + string.Join("\n", rows.Select(row => $"- {row.Message} — {TimeZoneInfo.ConvertTime(row.At, PlanningEngine.Zone(preference.TimeZoneId)):HH:mm dd/MM/yyyy}{(row.Due ? " (đã đến giờ)" : "")}."));

    private async Task ConfigureNoraReminderChannelsAsync(Guid uid, InAppNotification row, CancellationToken ct)
    {
        var settings=await db.NotificationSettings.SingleOrDefaultAsync(x=>x.UserId==uid,ct);
        row.IsVisible=settings?.InAppEnabled != false;
        row.EmailPending=settings?.EmailEnabled == true && row.EmailSentAt is null;
        if(row.EmailPending) row.NextEmailAttemptAt ??= row.ScheduledFor;
        var telegram=await db.TelegramConnections.SingleOrDefaultAsync(x=>x.UserId==uid && x.Enabled && x.ChatId != null,ct);
        if(telegram is not null && row.TelegramSentAt is null && (row.TelegramBindingId is null || row.TelegramBindingId == telegram.BindingId))
        {
            row.TelegramPending=true; row.TelegramBindingId=telegram.BindingId;
            row.NextTelegramAttemptAt ??= row.ScheduledFor;
        }
        else row.TelegramPending=false;
    }

    private async Task RefreshNoraReminderChannelsAsync(Guid uid, CancellationToken ct)
    {
        await MigrateInboxRemindersAsync(uid,ct);
        foreach(var row in await Mine<InAppNotification>(uid).Where(x=>x.Kind=="NORA_REMINDER" && x.ReadAt==null && x.ScheduledFor>=Now.AddDays(-1)).ToArrayAsync(ct))
            await ConfigureNoraReminderChannelsAsync(uid,row,ct);
    }
}
