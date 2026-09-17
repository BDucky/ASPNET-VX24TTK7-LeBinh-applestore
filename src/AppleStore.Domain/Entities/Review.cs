namespace AppleStore.Domain.Entities;

public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string? Content { get; set; }
    public bool HasMedia { get; set; }
    public bool PurchaseVerified { get; set; }
    public bool Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
}
