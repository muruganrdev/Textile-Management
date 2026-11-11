using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class OrderReturnConfiguration : IEntityTypeConfiguration<OrderReturn>
{
    public void Configure(EntityTypeBuilder<OrderReturn> builder)
    {
        builder.ToTable("OrderReturns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.IsStockRestored)
            .HasDefaultValue(false);

        builder.Property(r => r.RefundAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0.00m);

        builder.Property(r => r.AdminNotes)
            .HasMaxLength(500);

        builder.Property(r => r.RequestedAtUtc)
            .IsRequired();

        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.Order)
            .WithMany(o => o.Returns)
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Customer)
            .WithMany(u => u.SubmittedReturns)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ProcessedByUser)
            .WithMany(u => u.ProcessedReturns)
            .HasForeignKey(r => r.ProcessedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
