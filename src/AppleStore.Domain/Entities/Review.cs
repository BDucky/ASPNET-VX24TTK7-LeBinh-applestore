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

    // Added 2026-10-08: the report lets staff reply to a review, and its
    // schema has nowhere to keep the reply. UpdatedAt marks an edited review.
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
}
