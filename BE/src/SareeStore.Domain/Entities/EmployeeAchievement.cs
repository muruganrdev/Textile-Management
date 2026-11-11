using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class EmployeeAchievement : BaseEntity
{
    public int EmployeeId { get; set; }
    public string AchievementTitle { get; set; } = string.Empty;
    public AchievementBadge AchievementBadge { get; set; } = AchievementBadge.Bronze;
    public int ThresholdOrders { get; set; }
    public int VerifiedOrdersCount { get; set; }
    public DateTime AwardedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User Employee { get; set; } = null!;
}
