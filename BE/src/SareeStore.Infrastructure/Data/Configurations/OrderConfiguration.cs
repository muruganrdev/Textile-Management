using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_StockRestored_Timestamp", "[IsStockRestored] = 0 OR [StockRestoredAtUtc] IS NOT NULL");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.SubTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.DiscountAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0.00m);

        builder.Property(o => o.ShippingFee)
            .HasPrecision(18, 2)
            .HasDefaultValue(0.00m);

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.OrderStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.ShippingRecipientName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(o => o.ShippingPhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.ShippingAddressLine1)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(o => o.ShippingAddressLine2)
            .HasMaxLength(250);

        builder.Property(o => o.ShippingCity)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.ShippingState)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.ShippingPostalCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.ShippingCountry)
            .HasMaxLength(100)
            .HasDefaultValue("India")
            .IsRequired();

        builder.Property(o => o.OrderDateUtc)
            .IsRequired();

        builder.Property(o => o.IsStockRestored)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(o => o.StockRestoredAtUtc);

        builder.Property(o => o.CancellationReason)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.OrderStatus);
        builder.HasIndex(o => o.OrderDateUtc);

        // Relationships
        builder.HasOne(o => o.Customer)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Coupon)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CouponId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Payments)
            .WithOne(p => p.Order)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.StatusHistories)
            .WithOne(h => h.Order)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Returns)
            .WithOne(r => r.Order)
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
