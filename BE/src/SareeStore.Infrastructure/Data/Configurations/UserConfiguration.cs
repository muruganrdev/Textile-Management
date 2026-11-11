using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);
    }
}
