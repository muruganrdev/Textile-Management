using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories", t =>
        {
            t.HasCheckConstraint("CK_Inventories_StockQuantity_NonNegative", "[StockQuantity] >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.StockQuantity)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(i => i.LowStockThreshold)
            .HasDefaultValue(2)
            .IsRequired();

        builder.Property(i => i.RowVersion)
            .IsRowVersion();

        builder.HasIndex(i => i.ProductId)
            .IsUnique();

        builder.HasOne(i => i.Product)
            .WithOne(p => p.Inventory)
            .HasForeignKey<Inventory>(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
