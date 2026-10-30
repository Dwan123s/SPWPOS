namespace SPWPOS.Domain.Entities;

public sealed class GoogleCalendarConnection : OwnedRecord
{
    public string GoogleSubject { get; set; } = "";
    public string Email { get; set; } = "";
    public string ProtectedRefreshToken { get; set; } = "";
    public string CalendarId { get; set; } = "";
    public string Status { get; set; } = "Connected";
    public string LastError { get; set; } = "";
    public string? SyncToken { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset NextSyncAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public int FailureCount { get; set; }
    public int ReminderMinutes { get; set; } = 15;
    public bool RemindAtStart { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
