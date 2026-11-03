using System.Globalization;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Telegram;

public sealed record TelegramReminderDetails(string? Name = null, DateTimeOffset? Start = null,
    DateTimeOffset? End = null, DateTimeOffset? Deadline = null);

public static class TelegramMessageFormatter
{
    private static readonly string[] Days = ["Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy"];
    // Escape only Telegram-supported entities; retain Vietnamese characters and emoji.
    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    private static string Clock(DateTimeOffset at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);
    private static string Date(DateTimeOffset at) => $"{Days[(int)at.DayOfWeek]}, {at.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
    private static string Duration(double minutes)
    {
        var total = Math.Max(1, (int)Math.Ceiling(minutes));
        if (total < 60) return $"{total} phút";
        if (total < 1440) return $"{total / 60} giờ" + (total % 60 > 0 ? $" {total % 60} phút" : "");
        return $"{total / 1440} ngày" + (total % 1440 >= 60 ? $" {total % 1440 / 60} giờ" : "");
    }
    private static string ZoneLabel(TimeZoneInfo zone, DateTimeOffset reference)
    {
        var offset = TimeZoneInfo.ConvertTime(reference, zone).Offset;
        var clock = $"UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm}";
        return zone.Id is "Asia/Ho_Chi_Minh" or "Asia/Saigon" or "SE Asia Standard Time" ? $"Giờ Việt Nam ({clock})" : $"{zone.Id.Replace('_', ' ')} ({clock})";
    }
    public static string Format(InAppNotification notification, TelegramReminderDetails details, string timeZoneId, DateTimeOffset now)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        var symbol = notification.Kind switch {
            "EVENT_STARTED" or "SESSION_STARTED" => "🟢", "DEADLINE" => "⏰", "SESSION_REMINDER" => "🎯", "SESSION_REVIEW" => "📝", "WORKLOAD_WARNING" => "⚠️",
            "SCHEDULE_CHANGE" or "SCHEDULE_CHANGED" => "🗓", "AUTO_HEAL" => "✅", _ => "🔔",
        };
        var message = $"{symbol} <b>{Escape(notification.Title)}</b>\n\n";
        if (!string.IsNullOrWhiteSpace(details.Name)) message += $"<b>{Escape(details.Name)}</b>\n";
        if (details.Deadline is { } deadline)
        {
            var local = TimeZoneInfo.ConvertTime(deadline, zone);
            message += $"<blockquote>📅 Hạn chót: {Date(local)}\n🕒 <b>{Clock(local)}</b></blockquote>\n\n";
            message += $"⌛ Còn {Duration((deadline - now).TotalMinutes)} để hoàn thành";
        }
        else if (details.Start is { } start)
        {
            var localStart = TimeZoneInfo.ConvertTime(start, zone);
            var time = Clock(localStart);
            if (details.End is { } end)
            {
                var localEnd = TimeZoneInfo.ConvertTime(end, zone);
                time += localStart.Date == localEnd.Date ? $" – {Clock(localEnd)}" : $" → {Clock(localEnd)} ({localEnd.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})";
            }
            message += $"<blockquote>📅 {Date(localStart)}\n🕒 <b>{time}</b>";
            if (details.End is { } finish && finish > start) message += $"\n⏱ Thời lượng: {Duration((finish - start).TotalMinutes)}";
            message += "</blockquote>\n\n";
            message += notification.Kind == "SESSION_REVIEW" ? "Bạn đã làm xong chưa? Mở Hôm nay trong PLANORA: chọn Đã xong công việc, hoặc Cần thêm thời gian để xác nhận phiên bổ sung."
                : start > now ? $"⏳ Bắt đầu sau {Duration((start - now).TotalMinutes)}"
                : "🟢 Đang diễn ra";
        }
        else message += $"<blockquote>{Escape(notification.Message)}</blockquote>";
        return message + $"\n\n<i>PLANORA · {Escape(ZoneLabel(zone, details.Deadline ?? details.Start ?? now))}</i>";
    }
}
