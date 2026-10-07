namespace AppleStore.Web.Models.Cart;

// Quantity is nullable so "abc" or an empty box binds to null and is refused
// as an invalid quantity rather than becoming 0 silently.
public sealed class CartAddForm
{
    public int VariantId { get; set; }
    public int? Quantity { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class CartLineForm
{
    public int ItemId { get; set; }
    public int? Quantity { get; set; }
}
