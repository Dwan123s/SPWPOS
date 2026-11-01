using System.Xml.Linq;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Telegram;
namespace SPWPOS.UnitTests;

public sealed class TelegramMessageFormatterTests
{
    [Fact]
    public void Event_message_separates_name_date_time_duration_and_countdown_in_account_timezone()
    {
        var now = DateTimeOffset.Parse("2026-11-01T11:00:00Z");
        var message = TelegramMessageFormatter.Format(new() { Kind = "EVENT_REMINDER", Title = "Sắp đến lịch hẹn", Message = "Old compact message (Asia/Ho_Chi_Minh)." },
            new("Đi làm", now.AddMinutes(5), now.AddMinutes(95)), "Asia/Ho_Chi_Minh", now);
        Assert.StartsWith("🔔 <b>Sắp đến lịch hẹn</b>\n\n<b>Đi làm</b>", message);
        Assert.Contains("📅 Chủ nhật, 01/11/2026", message);
        Assert.Contains("🕒 <b>18:05 – 19:35</b>", message);
        Assert.Contains("Thời lượng: 1 giờ 30 phút", message);
        Assert.Contains("Bắt đầu sau 5 phút", message);
        Assert.Contains("<i>PLANORA · Giờ Việt Nam (UTC+07:00)</i>", message);
        Assert.DoesNotContain("Old compact", message); Assert.DoesNotContain("Asia/Ho_Chi_Minh", message);
        AssertSupportedMarkup(message);
    }

    [Fact]
    public void Deadline_uses_deadlines_dst_offset_and_a_clear_remaining_time()
    {
        var now = DateTimeOffset.Parse("2026-10-31T18:00:00Z");
        var deadline = DateTimeOffset.Parse("2026-11-01T06:30:00Z");
        var message = TelegramMessageFormatter.Format(new() { Kind = "DEADLINE", Title = "Công việc sắp đến hạn" },
            new("Nộp báo cáo", Deadline: deadline), "America/New_York", now);
        Assert.StartsWith("⏰ <b>Công việc sắp đến hạn</b>", message);
        Assert.Contains("Hạn chót: Chủ nhật, 01/11/2026", message); Assert.Contains("<b>01:30</b>", message);
        Assert.Contains("Còn 12 giờ 30 phút để hoàn thành", message);
        Assert.Contains("America/New York (UTC−05:00)", message);
        Assert.DoesNotContain("Bắt đầu sau", message); AssertSupportedMarkup(message);
    }

    [Fact]
    public void Overnight_session_shows_the_end_date_instead_of_a_reversed_time_range()
    {
        var start = DateTimeOffset.Parse("2026-11-01T16:30:00Z");
        var message = TelegramMessageFormatter.Format(new() { Kind = "SESSION_REMINDER", Title = "Sắp đến phiên làm việc" },
            new("Ôn tập", start, start.AddHours(1)), "Asia/Ho_Chi_Minh", start.AddMinutes(-10));
        Assert.Contains("23:30 → 00:30 (02/11/2026)", message);
        Assert.Contains("Bắt đầu sau 10 phút", message); AssertSupportedMarkup(message);
    }

    [Fact]
    public void User_text_cannot_inject_formatting_or_links_and_vietnamese_stays_readable()
    {
        var now = DateTimeOffset.Parse("2026-11-01T11:00:00Z");
        var message = TelegramMessageFormatter.Format(new() { Kind = "EVENT_REMINDER", Title = "Nhắc <b>lịch</b> & việc" },
            new("Họp <a href=\"https://example.com\">nhóm</a> & kế hoạch", now.AddMinutes(5), now.AddMinutes(35)), "UTC", now);
        Assert.Contains("Nhắc &lt;b&gt;lịch&lt;/b&gt; &amp; việc", message);
        Assert.Contains("Họp &lt;a href=&quot;https://example.com&quot;&gt;nhóm&lt;/a&gt; &amp; kế hoạch", message);
        Assert.DoesNotContain("<a href", message); AssertSupportedMarkup(message);
    }

    [Fact]
    public void Generic_warnings_and_missing_source_details_keep_the_original_message_without_inventing_times()
    {
        var message = TelegramMessageFormatter.Format(new() { Kind = "WORKLOAD_WARNING", Title = "Kế hoạch cần được xem lại", Message = "Lịch & deadline cần điều chỉnh." },
            new(), "Asia/Ho_Chi_Minh", DateTimeOffset.Parse("2026-11-01T11:00:00Z"));
        Assert.StartsWith("⚠️ <b>Kế hoạch cần được xem lại</b>", message);
        Assert.Contains("<blockquote>Lịch &amp; deadline cần điều chỉnh.</blockquote>", message);
        Assert.DoesNotContain("Bắt đầu", message); Assert.DoesNotContain("Thời lượng", message); AssertSupportedMarkup(message);
    }

    [Fact]
    public void Reminders_at_start_and_session_review_use_matching_status_instead_of_a_negative_countdown()
    {
        var now = DateTimeOffset.Parse("2026-11-01T11:00:00Z");
        var details = new TelegramReminderDetails("Đi làm", now.AddMinutes(-1), now.AddMinutes(29));
        var live = TelegramMessageFormatter.Format(new() { Kind = "EVENT_REMINDER", Title = "Lịch hẹn đang bắt đầu" }, details, "Asia/Ho_Chi_Minh", now);
        Assert.Contains("🟢 Đang diễn ra", live); Assert.DoesNotContain("Bắt đầu sau", live);
        var review = TelegramMessageFormatter.Format(new() { Kind = "SESSION_REVIEW", Title = "Bạn đã hoàn thành phiên này chưa?" }, details, "Asia/Ho_Chi_Minh", now);
        Assert.Contains("Cập nhật kết quả phiên này trong PLANORA nhé.", review); Assert.DoesNotContain("Đang diễn ra", review);
    }

    private static void AssertSupportedMarkup(string message)
    {
        var document = XDocument.Parse("<root>" + message + "</root>");
        Assert.All(document.Root!.Descendants(), element => Assert.Contains(element.Name.LocalName, new[] { "b", "i", "blockquote" }));
    }
}
