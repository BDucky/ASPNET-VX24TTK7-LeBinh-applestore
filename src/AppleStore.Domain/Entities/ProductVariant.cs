namespace AppleStore.Domain.Entities;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    // Null means the store has no price for this variant yet and the page
    // shows "Contact for price", as rauvang.com does. Never stored as 0, so a
    // missing price can't be summed into an order total by mistake.
    public decimal? Price { get; set; }
    public int StockQty { get; set; }
    public bool Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
}
