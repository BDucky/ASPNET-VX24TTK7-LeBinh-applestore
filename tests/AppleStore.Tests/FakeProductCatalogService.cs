using AppleStore.Infrastructure.Services;

namespace AppleStore.Tests;

public class FakeProductCatalogService : IProductCatalogService
{
    public IReadOnlyList<ProductSummary> ProductsToReturn { get; set; } = Array.Empty<ProductSummary>();
    public ProductDetail? DetailToReturn { get; set; }

    public string? LastCategorySlug { get; private set; }
    public string? LastQuery { get; private set; }
    public string? LastSlugRequested { get; private set; }

    public Task<IReadOnlyList<ProductSummary>> GetProductsAsync(string? categorySlug = null, string? query = null, CancellationToken ct = default)
    {
        LastCategorySlug = categorySlug;
        LastQuery = query;
        return Task.FromResult(ProductsToReturn);
    }

    public Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        LastSlugRequested = slug;
        return Task.FromResult(DetailToReturn);
    }
}
