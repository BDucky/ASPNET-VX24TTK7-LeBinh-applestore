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
}
