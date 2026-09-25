namespace AppleStore.Infrastructure.Services;

public enum ProductSort
{
    Featured,
    PriceAscending,
    PriceDescending,
}

public interface IProductCatalogService
{
    // Active products only (Product.Status && at least reflects catalog
    // visibility). categorySlug and query are both optional and combine
    // with AND when both are given.
    Task<IReadOnlyList<ProductSummary>> GetProductsAsync(string? categorySlug = null, string? query = null, ProductSort sort = ProductSort.Featured, CancellationToken ct = default);

    // Null for an unknown slug or an inactive product, same "not found"
    // either way: an inactive product isn't reachable by direct link.
    Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct = default);

    // Null when the SKU doesn't exist, doesn't belong to the product at
    // productSlug, or either the variant or its parent product is inactive.
    // productSlug is required, not just a courtesy: a SKU is unique on its
    // own, but the route is /Products/{slug}/{sku}, and a mismatched pair
    // (a real SKU under the wrong product's URL) must 404, not silently
    // serve the variant under someone else's product page.
    Task<ProductVariantDetail?> GetVariantAsync(string productSlug, string sku, CancellationToken ct = default);
}
