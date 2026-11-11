using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(250);

        builder.Property(c => c.DiscountType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.DiscountValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.MinOrderAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.MaxDiscountCap)
            .HasPrecision(18, 2);

        builder.Property(c => c.TimesUsed)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(c => c.StartDateUtc)
            .IsRequired();

        builder.Property(c => c.EndDateUtc)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(c => c.Code)
            .IsUnique();

        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Coupon)
            .HasForeignKey(o => o.CouponId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
