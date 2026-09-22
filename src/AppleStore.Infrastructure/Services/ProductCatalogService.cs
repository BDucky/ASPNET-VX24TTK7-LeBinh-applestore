using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class ProductCatalogService : IProductCatalogService
{
    private readonly AppDbContext _db;

    public ProductCatalogService(AppDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<ProductSummary>> GetProductsAsync(string? categorySlug = null, string? query = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
