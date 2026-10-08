using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class AdminCatalogService : IAdminCatalogService
{
    private readonly AppDbContext _db;
    private readonly IImageLibrary _images;
    private readonly TimeProvider _time;

    public AdminCatalogService(AppDbContext db, IImageLibrary images, TimeProvider time)
    {
        _db = db;
        _images = images;
        _time = time;
    }

    public Task<IReadOnlyList<ProductAdminRow>> ListAsync(string? search, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ProductEdit?> GetAsync(int productId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AdminCatalogResult> CreateAsync(ProductInput input, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AdminCatalogResult> UpdateAsync(int productId, ProductInput input, long version, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AdminCatalogResult> SetOnSaleAsync(int productId, bool onSale, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AdminCatalogResult> AddVariantAsync(int productId, VariantInput input, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AdminCatalogResult> UpdateVariantAsync(int variantId, VariantChange change, CancellationToken ct = default) => throw new NotImplementedException();
}
