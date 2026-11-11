using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class Address : BaseEntity
{
    public int UserId { get; set; }
    public AddressType AddressType { get; set; } = AddressType.Shipping;
    public string RecipientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public bool IsDefault { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
