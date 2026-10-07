namespace AppleStore.Infrastructure.Services;

// FromPrice is the lowest active variant price. Null when no variant has a
// price yet, which the storefront shows as "Contact for price".
public record ProductSummary(int Id, string Name, string Slug, string CategoryName, string CategorySlug, decimal? FromPrice, string? ImageUrl);

// One card on a model page: a storage/size/connectivity configuration, the
// way rauvang.com groups its variants (for example "iPhone 18 Pro Max 256GB
// ( VN )"). Its colours and regions are chosen on the configuration's page.
public record ConfigurationSummary(string Name, string Slug, decimal? FromPrice, bool InStock);

public record ProductDetail(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string CategoryName,
    string CategorySlug,
    IReadOnlyList<ConfigurationSummary> Configurations,
    IReadOnlyList<string> ImageUrls)
{
    public decimal? FromPrice => Configurations.Select(c => c.FromPrice).Min();
}

// One purchasable variant inside a configuration: a colour and region pair.
// VariantId is what the cart form posts: SKUs are not unique across variants.
public record VariantChoice(int VariantId, string SKU, string? Color, string? Region, decimal? Price, int StockQty);

public record ConfigurationDetail(
    int ProductId,
    string ProductName,
    string ProductSlug,
    string CategoryName,
    string CategorySlug,
    string? ImageUrl,
    string Name,
    string Slug,
    IReadOnlyList<VariantChoice> Choices)
{
    // The variant a page opens on. An exact colour and region match wins,
    // then the first choice in that colour, then the first choice overall
    // (rauvang.com opens on its first listed colour).
    public VariantChoice Select(string? color, string? region)
    {
        return Choices.FirstOrDefault(c => Same(c.Color, color) && Same(c.Region, region))
            ?? Choices.FirstOrDefault(c => Same(c.Color, color))
            ?? Choices[0];
    }

    private static bool Same(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
