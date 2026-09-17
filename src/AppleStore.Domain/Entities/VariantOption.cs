namespace AppleStore.Domain.Entities;

// Composite primary key (VariantId, OptionTypeId): a variant has exactly one
// value per option type. OptionValueId is a plain FK, not part of the key.
public class VariantOption
{
    public int VariantId { get; set; }
    public int OptionTypeId { get; set; }
    public int OptionValueId { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public OptionType OptionType { get; set; } = null!;
    public OptionValue OptionValue { get; set; } = null!;
}
