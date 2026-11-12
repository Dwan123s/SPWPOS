namespace SPWPOS.Domain.Entities;

public sealed class ProjectSourceDocument
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string MimeType { get; set; } = "";
    public byte[] Content { get; set; } = [];
}
