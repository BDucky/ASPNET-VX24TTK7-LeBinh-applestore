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

    // One configuration (a card on the model page) with its colour and region
    // choices. Null when the configuration slug doesn't exist, belongs to a
    // different product, or the product or all its variants are inactive. A
    // real configuration under the wrong product's URL must 404, not be served
    // under someone else's page.
    Task<ConfigurationDetail?> GetConfigurationAsync(string productSlug, string configurationSlug, CancellationToken ct = default);
}
