namespace SPWPOS.Domain.Entities;

public sealed class CalendarOAuthRequest : OwnedRecord
{
    public string StateHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public int ReminderMinutes { get; set; }
    public bool RemindAtStart { get; set; }
}
