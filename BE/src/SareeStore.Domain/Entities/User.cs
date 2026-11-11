using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public Cart? Cart { get; set; }
    public Wishlist? Wishlist { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<OrderStatusHistory> ProcessedOrderHistories { get; set; } = new List<OrderStatusHistory>();
    public ICollection<OrderReturn> SubmittedReturns { get; set; } = new List<OrderReturn>();
    public ICollection<OrderReturn> ProcessedReturns { get; set; } = new List<OrderReturn>();
    public ICollection<EmployeeAchievement> Achievements { get; set; } = new List<EmployeeAchievement>();
}
