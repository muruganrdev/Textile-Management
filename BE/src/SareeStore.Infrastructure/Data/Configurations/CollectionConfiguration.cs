using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("Collections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Slug)
            .HasMaxLength(170)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.BannerImageUrl)
            .HasMaxLength(500);

        builder.Property(c => c.IsFeatured)
            .HasDefaultValue(false);

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(c => c.Slug)
            .IsUnique();

        builder.HasMany(c => c.ProductCollections)
            .WithOne(pc => pc.Collection)
            .HasForeignKey(pc => pc.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
