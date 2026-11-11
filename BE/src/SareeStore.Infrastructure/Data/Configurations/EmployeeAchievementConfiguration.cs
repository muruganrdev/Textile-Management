using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SareeStore.Domain.Entities;

namespace SareeStore.Infrastructure.Data.Configurations;

public class EmployeeAchievementConfiguration : IEntityTypeConfiguration<EmployeeAchievement>
{
    public void Configure(EntityTypeBuilder<EmployeeAchievement> builder)
    {
        builder.ToTable("EmployeeAchievements");

        builder.HasKey(ea => ea.Id);

        builder.Property(ea => ea.AchievementTitle)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ea => ea.AchievementBadge)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ea => ea.ThresholdOrders)
            .IsRequired();

        builder.Property(ea => ea.VerifiedOrdersCount)
            .IsRequired();

        builder.Property(ea => ea.AwardedAtUtc)
            .IsRequired();

        builder.HasIndex(ea => new { ea.EmployeeId, ea.AchievementBadge })
            .IsUnique();

        builder.HasOne(ea => ea.Employee)
            .WithMany(u => u.Achievements)
            .HasForeignKey(ea => ea.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
