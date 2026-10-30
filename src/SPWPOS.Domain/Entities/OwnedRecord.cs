namespace SPWPOS.Domain.Entities;

public abstract class OwnedRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
}
