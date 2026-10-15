namespace SPWPOS.Domain.Entities;
public sealed class RecoverySetting : OwnedRecord
{
    public bool Enabled { get; set; } = true;
    public bool AutoApply { get; set; }
    public int MissedDays { get; set; } = 2;
    public int QuietStartMinute { get; set; } = 1320;
    public int QuietEndMinute { get; set; } = 420;
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class RecoveryCase : OwnedRecord
{
    public string DedupeKey { get; set; } = "";
    public string TaskIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "PROPOSED";
    public string Message { get; set; } = "";
    public Guid Revision { get; set; }
    public Guid? PlanId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
