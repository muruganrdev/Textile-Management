using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.SKU)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(p => p.Slug)
            .HasMaxLength(280)
            .IsRequired();

        builder.Property(p => p.Description)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.DiscountPrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.Fabric)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.WeaveType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ZariType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.PrimaryColor)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.SareeLengthMeters)
            .HasPrecision(4, 2)
            .HasDefaultValue(5.50m)
            .IsRequired();

        builder.Property(p => p.SareeWidthInches)
            .HasPrecision(4, 2)
            .HasDefaultValue(48.00m)
            .IsRequired();

        builder.Property(p => p.BlouseIncluded)
            .HasDefaultValue(true);

        builder.Property(p => p.BlouseLengthMeters)
            .HasPrecision(4, 2)
            .HasDefaultValue(0.80m);

        builder.Property(p => p.CraftDetails)
            .HasMaxLength(500);

        builder.Property(p => p.Occasion)
            .HasMaxLength(100);

        builder.Property(p => p.Has3DModel)
            .HasDefaultValue(false);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        // Indexes
        builder.HasIndex(p => p.SKU)
            .IsUnique();

        builder.HasIndex(p => p.Slug)
            .IsUnique();

        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.Price);

        // Relationships
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Inventory)
            .WithOne(i => i.Product)
            .HasForeignKey<Inventory>(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Product3DModel)
            .WithOne(m => m.Product)
            .HasForeignKey<Product3DModel>(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne(img => img.Product)
            .HasForeignKey(img => img.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ProductCollections)
            .WithOne(pc => pc.Product)
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.CartItems)
            .WithOne(ci => ci.Product)
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.WishlistItems)
            .WithOne(wi => wi.Product)
            .HasForeignKey(wi => wi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.OrderItems)
            .WithOne(oi => oi.Product)
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
