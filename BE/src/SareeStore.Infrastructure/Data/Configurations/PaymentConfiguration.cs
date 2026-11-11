using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.TransactionId)
            .HasMaxLength(100);

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.FailureReason)
            .HasMaxLength(500);

        builder.Property(p => p.IdempotencyKey)
            .HasMaxLength(100);

        builder.Property(p => p.PaymentProvider)
            .HasMaxLength(50)
            .HasDefaultValue("Simulated")
            .IsRequired();

        builder.Property(p => p.IsLateSuccess)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.RequiresRefund)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.RefundReason)
            .HasMaxLength(500);

        builder.HasIndex(p => p.OrderId, "IX_Payments_OrderId");

        // Filtered unique index: allows nulls while enforcing uniqueness on non-null transaction IDs
        builder.HasIndex(p => p.TransactionId)
            .IsUnique()
            .HasFilter("[TransactionId] IS NOT NULL");

        // Filtered unique index: prevents more than one successful payment record per order
        builder.HasIndex(p => p.OrderId, "IX_Payments_OrderId_Paid")
            .IsUnique()
            .HasFilter("[PaymentStatus] = 'Paid'");

        // Filtered unique index: enforces at most one active Processing attempt per order
        builder.HasIndex(p => p.OrderId, "IX_Payments_OrderId_Processing")
            .IsUnique()
            .HasFilter("[PaymentStatus] = 'Processing'");

        // Filtered unique index: prevents duplicate payment initiation for the same order and idempotency key
        builder.HasIndex(p => new { p.OrderId, p.IdempotencyKey }, "IX_Payments_OrderId_IdempotencyKey")
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasOne(p => p.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
