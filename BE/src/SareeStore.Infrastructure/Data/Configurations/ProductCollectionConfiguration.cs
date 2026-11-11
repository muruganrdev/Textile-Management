using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class ProductCollectionConfiguration : IEntityTypeConfiguration<ProductCollection>
{
    public void Configure(EntityTypeBuilder<ProductCollection> builder)
    {
        builder.ToTable("ProductCollections");

        builder.HasKey(pc => new { pc.ProductId, pc.CollectionId });

        builder.HasIndex(pc => pc.CollectionId);

        builder.HasOne(pc => pc.Product)
            .WithMany(p => p.ProductCollections)
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pc => pc.Collection)
            .WithMany(c => c.ProductCollections)
            .HasForeignKey(pc => pc.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
