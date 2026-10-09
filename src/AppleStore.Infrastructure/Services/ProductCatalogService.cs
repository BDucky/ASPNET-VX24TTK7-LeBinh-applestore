using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

public class ProductCatalogService : IProductCatalogService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public ProductCatalogService(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(string? categorySlug = null, string? query = null, ProductSort sort = ProductSort.Featured,
        PriceBand band = PriceBand.Any, CancellationToken ct = default)
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

        var list = await products.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
        var productIds = list.Select(p => p.Id).ToList();

        // Sale prices (SalePrices), so a promotion shows on the card and
        // counts for the bands. With a band, only prices inside it count: a
        // card shows its lowest price in the band and a product with none
        // drops out.
        var range = PriceBands.Find(band);
        var prices = await SalePrices.LoadAsync(_db, _time.GetUtcNow().UtcDateTime, ct);
        var priced = await _db.ProductVariants
            .Where(v => v.Status && v.Price != null && productIds.Contains(v.ProductId))
            .OrderBy(v => v.Id)
            .Select(v => new { v.ProductId, v.Price })
            .ToListAsync(ct);
        var lowestActivePrices = priced
            .Select(v => (v.ProductId, Sale: prices.For(v.ProductId, v.Price)))
            .Where(v => range is null || ((range.Min is null || v.Sale.Price >= range.Min) && (range.Max is null || v.Sale.Price < range.Max)))
            .GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.MinBy(v => v.Sale.Price).Sale);

        var firstImages = await _db.ProductImages
            .Where(i => productIds.Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, ImageUrl = g.OrderBy(i => i.SortOrder).First().ImageUrl })
            .ToDictionaryAsync(x => x.ProductId, x => x.ImageUrl, ct);

        if (range is not null)
            list = list.Where(p => lowestActivePrices.ContainsKey(p.Id)).ToList();
        if (sort == ProductSort.Newest)
            list = list.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToList();

        var summaries = list
            .Select(p => new ProductSummary(
                p.Id,
                p.Name,
                p.Slug,
                p.Category.Name,
                p.Category.Slug,
                lowestActivePrices.GetValueOrDefault(p.Id)?.Price,
                firstImages.GetValueOrDefault(p.Id),
                lowestActivePrices.GetValueOrDefault(p.Id)?.WasPrice));

        // "Contact for price" models go last in both price sorts: they have
        // no price to rank by, and putting them first would bury real prices.
        summaries = sort switch
        {
            ProductSort.PriceAscending => summaries.OrderBy(p => p.FromPrice is null).ThenBy(p => p.FromPrice),
            ProductSort.PriceDescending => summaries.OrderBy(p => p.FromPrice is null).ThenByDescending(p => p.FromPrice),
            _ => summaries,
        };

        return summaries.ToList();
    }

    public async Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status, ct);

        if (product is null)
            return null;

        var configurations = (await LoadConfigurationsAsync(_db, product, await SalePricesAsync(ct), ct))
            .Select(g =>
            {
                var cheapest = g.Choices.Where(c => c.Price is not null).MinBy(c => c.Price);
                return new ConfigurationSummary(g.Name, g.Slug, cheapest?.Price, g.Choices.Any(c => c.StockQty > 0), cheapest?.WasPrice);
            })
            .ToList();

        return new ProductDetail(product.Id, product.Name, product.Slug, product.Description, product.Category.Name, product.Category.Slug, configurations, await ImageUrlsAsync(_db, product.Id, ct));
    }

    public async Task<ConfigurationDetail?> GetConfigurationAsync(string productSlug, string configurationSlug, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Slug == productSlug && p.Status, ct);

        if (product is null)
            return null;

        var configuration = (await LoadConfigurationsAsync(_db, product, await SalePricesAsync(ct), ct)).FirstOrDefault(g => g.Slug == configurationSlug);
        if (configuration is null)
            return null;

        var imageUrl = (await ImageUrlsAsync(_db, product.Id, ct)).FirstOrDefault();
        return new ConfigurationDetail(product.Id, product.Name, product.Slug, product.Category.Name, product.Category.Slug, imageUrl, configuration.Name, configuration.Slug, configuration.Choices);
    }

    internal sealed record ConfigurationGroup(string Name, string Slug, IReadOnlyList<VariantChoice> Choices);

    // Groups a product's active variants by their "config" option, in catalog
    // order (the order the variants were seeded, which follows the reference
    // store's listing). A variant with no "config" option belongs to one
    // configuration named after the product, for items sold as a single model.
    // Also used by CompareService, so both pages group variants the same way.
    internal static async Task<IReadOnlyList<ConfigurationGroup>> LoadConfigurationsAsync(AppDbContext db, Domain.Entities.Product product, SalePrices prices, CancellationToken ct)
    {
        var variants = await db.ProductVariants
            .Where(v => v.ProductId == product.Id && v.Status)
            .OrderBy(v => v.Id)
            .ToListAsync(ct);
        var options = await VariantOptionLookup.LoadAsync(db, variants.Select(v => v.Id).ToList(), ct);
        string? Option(int variantId, string code) => options.Get(variantId, code);

        return variants
            .GroupBy(v => options.ConfigurationName(v.Id, product.Name))
            .Select(g => new ConfigurationGroup(
                g.Key,
                CatalogSlug.From(g.Key),
                g.Select(v =>
                {
                    var sale = prices.For(product.Id, v.Price);
                    return new VariantChoice(v.Id, v.SKU, Option(v.Id, "color"), Option(v.Id, "region"), sale.Price, v.StockQty, sale.WasPrice, sale.PromotionName);
                }).ToList()))
            .ToList();
    }

    private Task<SalePrices> SalePricesAsync(CancellationToken ct) => SalePrices.LoadAsync(_db, _time.GetUtcNow().UtcDateTime, ct);

    internal static Task<List<string>> ImageUrlsAsync(AppDbContext db, int productId, CancellationToken ct) =>
        db.ProductImages
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.SortOrder)
            .Select(i => i.ImageUrl)
            .ToListAsync(ct);
}
