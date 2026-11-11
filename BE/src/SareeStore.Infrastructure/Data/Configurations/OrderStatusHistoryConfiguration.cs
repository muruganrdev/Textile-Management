using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(h => h.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(h => h.Remarks)
            .HasMaxLength(500);

        builder.Property(h => h.ChangedAtUtc)
            .IsRequired();

        builder.HasIndex(h => h.OrderId);
        builder.HasIndex(h => h.ProcessedByUserId);

        builder.HasOne(h => h.Order)
            .WithMany(o => o.StatusHistories)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Nullable ProcessedByUserId supports customer-placed or system-automated status transitions
        builder.HasOne(h => h.ProcessedByUser)
            .WithMany(u => u.ProcessedOrderHistories)
            .HasForeignKey(h => h.ProcessedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
