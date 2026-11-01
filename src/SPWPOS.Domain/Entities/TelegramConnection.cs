namespace SPWPOS.Domain.Entities;

public sealed class TelegramConnection : OwnedRecord
{
    public long? ChatId { get; set; }
    public string? DisplayName { get; set; }
    public bool Enabled { get; set; }
    public Guid BindingId { get; set; } = Guid.NewGuid();
    public DateTimeOffset? ConnectedAt { get; set; }
    public string? LinkTokenHash { get; set; }
    public DateTimeOffset? LinkExpiresAt { get; set; }
    public string? ErrorCode { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public sealed class TelegramUpdateCursor
{
    public string Id { get; set; } = "";
    public long NextOffset { get; set; }
}
