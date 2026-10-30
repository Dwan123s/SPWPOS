using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Persistence;

public static class AgentRuntimeModel
{
    public static void Configure(ModelBuilder b)
    {
        var run = b.Entity<AgentRun>(); run.HasKey(x => x.Id);
        run.HasAlternateKey(x => new { x.UserId, x.Id });
        run.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        run.HasIndex(x => new { x.UserId, x.TurnId }).IsUnique();
        run.HasIndex(x => new { x.UserId, x.ConversationId, x.UpdatedAt });
        run.Property(x => x.Version).IsConcurrencyToken();
        run.Property(x => x.Status).HasMaxLength(32); run.Property(x => x.Goal).HasMaxLength(8000);
        var tool = b.Entity<AgentToolCall>(); tool.HasKey(x => x.Id);
        tool.HasOne<AgentRun>().WithMany().HasForeignKey(x => new { x.UserId, x.RunId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        tool.HasIndex(x => new { x.RunId, x.Sequence }).IsUnique();
        tool.Property(x => x.Tool).HasMaxLength(80); tool.Property(x => x.Status).HasMaxLength(24);
        tool.Property(x => x.ArgumentsHash).HasMaxLength(64); tool.Property(x => x.ResultCode).HasMaxLength(80);
    }
}
