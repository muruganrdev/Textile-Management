using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class Product3DModelConfiguration : IEntityTypeConfiguration<Product3DModel>
{
    public void Configure(EntityTypeBuilder<Product3DModel> builder)
    {
        builder.ToTable("Product3DModels");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.GlbModelUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(m => m.UsdzModelUrl)
            .HasMaxLength(500);

        builder.Property(m => m.PosterImageUrl)
            .HasMaxLength(500);

        builder.Property(m => m.FileSizeBytes)
            .IsRequired();

        builder.Property(m => m.CameraTarget)
            .HasMaxLength(100);

        builder.Property(m => m.CameraOrbit)
            .HasMaxLength(100);

        builder.Property(m => m.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(m => m.ProductId)
            .IsUnique();

        builder.HasOne(m => m.Product)
            .WithOne(p => p.Product3DModel)
            .HasForeignKey<Product3DModel>(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
