using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

// Maps to the report's "Attributes" table. Named AttributeDefinition instead of
// Attribute to avoid colliding with System.Attribute, which is implicitly in
// scope everywhere via ImplicitUsings.
public class AttributeDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AttributeDataType DataType { get; set; }
    public string? Unit { get; set; }
}
