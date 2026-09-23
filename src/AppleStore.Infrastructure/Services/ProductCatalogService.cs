using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

public class ProductCatalogService : IProductCatalogService
{
    private readonly AppDbContext _db;

    public ProductCatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(string? categorySlug = null, string? query = null, CancellationToken ct = default)
    {
        var products = _db.Products
            .Include(p => p.Category)
            .Where(p => p.Status);

        if (!string.IsNullOrWhiteSpace(categorySlug))
            products = products.Where(p => p.Category.Slug == categorySlug);

        if (!string.IsNullOrWhiteSpace(query))
            // EF.Functions.Like (not .Contains): SQLite's LIKE is case-insensitive
            // for ASCII by default, .Contains() translates to instr(), which isn't.
            products = products.Where(p => EF.Functions.Like(p.Name, $"%{query}%"));

        var list = await products.ToListAsync(ct);
        var productIds = list.Select(p => p.Id).ToList();

        var lowestActivePrices = await _db.ProductVariants
            .Where(v => v.Status && productIds.Contains(v.ProductId))
            .GroupBy(v => v.ProductId)
            .Select(g => new { ProductId = g.Key, MinPrice = g.Min(v => v.Price) })
            .ToDictionaryAsync(x => x.ProductId, x => x.MinPrice, ct);

        return list
            .Select(p => new ProductSummary(
                p.Id,
                p.Name,
                p.Slug,
                p.Category.Name,
                p.Category.Slug,
                lowestActivePrices.TryGetValue(p.Id, out var minPrice) ? minPrice : p.BasePrice,
                null))
            .ToList();
    }

    public async Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status, ct);

        if (product is null)
            return null;

        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == product.Id && v.Status)
            .OrderBy(v => v.Price)
            .Select(v => new ProductVariantSummary(v.Id, v.SKU, v.Price, v.StockQty))
            .ToListAsync(ct);

        var imageUrls = await _db.ProductImages
            .Where(i => i.ProductId == product.Id)
            .OrderBy(i => i.SortOrder)
            .Select(i => i.ImageUrl)
            .ToListAsync(ct);

        return new ProductDetail(product.Id, product.Name, product.Slug, product.Description, product.Category.Name, variants, imageUrls);
    }
}
