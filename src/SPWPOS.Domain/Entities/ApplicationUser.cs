using Microsoft.AspNetCore.Identity;
namespace SPWPOS.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public long ExperiencePoints { get; set; }
    public int UserLevel { get; set; } = 1;
    public string NoraSkin { get; set; } = "classic";
}
