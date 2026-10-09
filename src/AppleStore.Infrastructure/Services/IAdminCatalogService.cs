namespace AppleStore.Infrastructure.Services;

// Use cases 25-27: admins add, edit and take products off sale. "Delete" is
// taking off sale (agreed 2026-10-08): orders keep pointing at the product.
public interface IAdminCatalogService
{
    Task<IReadOnlyList<ProductAdminRow>> ListAsync(string? search, CancellationToken ct = default);
    Task<ProductEdit?> GetAsync(int productId, CancellationToken ct = default);
    Task<AdminCatalogResult> CreateAsync(ProductInput input, CancellationToken ct = default);
    Task<AdminCatalogResult> UpdateAsync(int productId, ProductInput input, long version, CancellationToken ct = default);
    Task<AdminCatalogResult> SetOnSaleAsync(int productId, bool onSale, CancellationToken ct = default);
    // userId: who made the change, for the price history (BM_PRICE_01).
    Task<AdminCatalogResult> AddVariantAsync(int productId, VariantInput input, int? userId = null, CancellationToken ct = default);
    Task<AdminCatalogResult> UpdateVariantAsync(int variantId, VariantChange change, int? userId = null, CancellationToken ct = default);
}
