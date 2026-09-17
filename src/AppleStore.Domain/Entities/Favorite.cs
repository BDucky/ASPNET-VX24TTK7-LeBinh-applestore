namespace AppleStore.Domain.Entities;

// Composite primary key (UserId, ProductId).
public class Favorite
{
    public int UserId { get; set; }
    public int ProductId { get; set; }

    public User User { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
