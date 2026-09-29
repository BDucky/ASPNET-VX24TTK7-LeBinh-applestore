namespace AppleStore.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    // Null when the store has no price for any of the product's variants
    // ("Contact for price"), never stored as 0. The storefront shows prices
    // from variants, not this column.
    public decimal? BasePrice { get; set; }
    public bool Status { get; set; }
    // Display order within a category, lowest first. Mirrors the order the
    // reference store (rauvang.com) lists its models in: newest first.
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
}
