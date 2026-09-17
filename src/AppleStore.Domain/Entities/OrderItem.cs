namespace AppleStore.Domain.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int VariantId { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
