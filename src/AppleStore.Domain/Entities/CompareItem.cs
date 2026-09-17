namespace AppleStore.Domain.Entities;

// Composite primary key (ListId, ProductId).
public class CompareItem
{
    public int ListId { get; set; }
    public int ProductId { get; set; }

    public CompareList CompareList { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
