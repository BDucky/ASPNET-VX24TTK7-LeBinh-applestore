namespace AppleStore.Domain.Entities;

public class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Label { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public bool IsDefault { get; set; }

    public User User { get; set; } = null!;
}
