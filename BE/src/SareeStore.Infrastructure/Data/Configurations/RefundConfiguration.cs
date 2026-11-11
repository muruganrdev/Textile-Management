using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", t =>
        {
            t.HasCheckConstraint("CK_Refunds_Amount_Positive", "[Amount] > 0");
            t.HasCheckConstraint("CK_Refunds_Currency_INR", "[Currency] = 'INR'");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("INR")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.RefundReference)
            .HasMaxLength(100);

        builder.Property(r => r.IdempotencyKey)
            .HasMaxLength(100);

        builder.Property(r => r.FailureReason)
            .HasMaxLength(500);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.Property(r => r.RefundedAtUtc);

        // Indexes
        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => r.PaymentId);

        // Unique filtered index on RefundReference
        builder.HasIndex(r => r.RefundReference)
            .IsUnique()
            .HasFilter("[RefundReference] IS NOT NULL");

        // Unique filtered index on PaymentId + IdempotencyKey to guarantee refund idempotency
        builder.HasIndex(r => new { r.PaymentId, r.IdempotencyKey }, "IX_Refunds_PaymentId_IdempotencyKey")
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        // Relationships
        builder.HasOne(r => r.Order)
            .WithMany(o => o.Refunds)
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Payment)
            .WithMany(p => p.Refunds)
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
