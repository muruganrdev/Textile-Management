using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.RecipientName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(a => a.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.AddressLine1)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(a => a.AddressLine2)
            .HasMaxLength(250);

        builder.Property(a => a.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.State)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.PostalCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Country)
            .HasMaxLength(100)
            .HasDefaultValue("India")
            .IsRequired();

        builder.Property(a => a.AddressType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.IsDefault)
            .HasDefaultValue(false);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.PostalCode);

        builder.HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
