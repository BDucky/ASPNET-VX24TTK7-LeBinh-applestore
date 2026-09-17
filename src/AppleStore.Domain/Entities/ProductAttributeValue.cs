namespace AppleStore.Domain.Entities;

// Junction/value table: composite primary key (ProductId, AttributeId).
public class ProductAttributeValue
{
    public int ProductId { get; set; }
    public int AttributeId { get; set; }
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }

    public Product Product { get; set; } = null!;
    public AttributeDefinition Attribute { get; set; } = null!;
}
