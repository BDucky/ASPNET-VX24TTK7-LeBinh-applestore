namespace AppleStore.Domain.Entities;

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
