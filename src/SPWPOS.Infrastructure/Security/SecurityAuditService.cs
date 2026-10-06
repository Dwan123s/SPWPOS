using SPWPOS.Application.Authentication.DTOs;
using SPWPOS.Application.Authentication.Interfaces;
using SPWPOS.Domain.Entities;
using SPWPOS.Domain.Enums;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Security;

public sealed class SecurityAuditService(ApplicationDbContext db, TimeProvider clock) : ISecurityAuditService
{
    public void Record(Guid? userId, SecurityEventType eventType, bool successful, RequestContext context, string? safeCode = null)
    {
        db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            UserId = userId,
            EventType = eventType,
            IsSuccessful = successful,
            CreatedAt = clock.GetUtcNow(),
            IpAddress = Clean(context.IpAddress, 45),
            UserAgent = Clean(context.UserAgent, 256),
            Metadata = Clean(safeCode, 100)
        });
    }
    private static string? Clean(string? value, int max) => value is null ? null : new string(value.Where(c => !char.IsControl(c)).Take(max).ToArray());
}
