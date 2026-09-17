using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }

    // Created/updated timestamps are normalized to DateTime across every entity
    // in this schema, including the two tables (Products, ProductVariants) where
    // the report loosely wrote "Date" instead of the DATETIME2(0) used elsewhere.
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
