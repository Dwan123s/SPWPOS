using Microsoft.EntityFrameworkCore;
using SPWPOS.Domain.Entities;
namespace SPWPOS.Infrastructure.Persistence;

public sealed class PetCommerceModel : IEntityTypeConfiguration<SkinPurchase>, IEntityTypeConfiguration<OwnedPetSkin>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SkinPurchase> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserId, x.Id });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.RequestId }).IsUnique(); b.HasIndex(x => x.OrderCode).IsUnique();
        b.HasIndex(x => x.PaymentLinkId).IsUnique().HasFilter("[PaymentLinkId] IS NOT NULL");
        b.HasIndex(x => x.ReceiptReference).IsUnique().HasFilter("[ReceiptReference] IS NOT NULL");
        b.HasIndex(x => new { x.UserId, x.SkinCode, x.Status });
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.SkinCode).HasMaxLength(40); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.PaymentLinkId).HasMaxLength(100); b.Property(x => x.CheckoutUrl).HasMaxLength(1000);
        b.Property(x => x.AccountNumber).HasMaxLength(100); b.Property(x => x.ReceiptReference).HasMaxLength(100);
    }
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<OwnedPetSkin> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.UserId, x.SkinCode }).IsUnique(); b.Property(x => x.SkinCode).HasMaxLength(40);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SkinPurchase>().WithMany().HasForeignKey(x => new { x.UserId, x.PurchaseId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
