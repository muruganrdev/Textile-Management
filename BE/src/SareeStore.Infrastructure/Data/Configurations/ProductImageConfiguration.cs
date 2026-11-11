using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");

        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.ImageUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(pi => pi.AltText)
            .HasMaxLength(250);

        builder.Property(pi => pi.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(pi => pi.IsPrimary)
            .HasDefaultValue(false);

        builder.HasIndex(pi => pi.ProductId);

        builder.HasOne(pi => pi.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
