using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");

        builder.HasKey(wi => wi.Id);

        builder.HasIndex(wi => new { wi.WishlistId, wi.ProductId })
            .IsUnique();

        builder.HasIndex(wi => wi.ProductId);

        builder.HasOne(wi => wi.Wishlist)
            .WithMany(w => w.Items)
            .HasForeignKey(wi => wi.WishlistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wi => wi.Product)
            .WithMany(p => p.WishlistItems)
            .HasForeignKey(wi => wi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
