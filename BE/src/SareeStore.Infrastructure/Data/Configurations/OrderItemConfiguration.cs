using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(oi => oi.Id);

        builder.Property(oi => oi.ProductTitleSnapshot)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(oi => oi.ProductSkuSnapshot)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(oi => oi.RegularPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(oi => oi.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(oi => oi.DiscountAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.Property(oi => oi.Quantity)
            .IsRequired();

        builder.Property(oi => oi.LineTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(oi => oi.OrderId);
        builder.HasIndex(oi => oi.ProductId);

        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oi => oi.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
