namespace AppleStore.Infrastructure.Services;

public record ProductSummary(int Id, string Name, string Slug, string CategoryName, string CategorySlug, decimal FromPrice);

public record ProductVariantSummary(int Id, string SKU, decimal Price, int StockQty);

public record ProductDetail(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string CategoryName,
    IReadOnlyList<ProductVariantSummary> Variants,
    IReadOnlyList<string> ImageUrls);
