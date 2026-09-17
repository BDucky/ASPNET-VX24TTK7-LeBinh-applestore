namespace AppleStore.Domain.Entities;

// Junction table: composite primary key (CategoryId, AttributeId), no separate Id.
public class CategoryAttribute
{
    public int CategoryId { get; set; }
    public int AttributeId { get; set; }

    public Category Category { get; set; } = null!;
    public AttributeDefinition Attribute { get; set; } = null!;
}
