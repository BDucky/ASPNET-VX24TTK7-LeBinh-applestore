using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Use case 10. The list itself is kept by the caller (a cookie, so a visitor
// can compare without an account); this service decides what may join it
// and builds the columns. A product hidden since it was added drops out and
// frees its slot, so the cap counts only products the shopper can see.
public class CompareService : ICompareService
{
    // Three, like Apple's own compare page (owner's decision 2026-10-08).
    public const int MaxItems = 3;

    private readonly AppDbContext _db;
    private readonly IReviewService _reviews;
    private readonly TimeProvider _time;

    public CompareService(AppDbContext db, IReviewService reviews, TimeProvider? time = null)
    {
        _db = db;
        _reviews = reviews;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<CompareColumn>> BuildAsync(IReadOnlyList<int> productIds, CancellationToken ct = default)
    {
        var ids = await VisibleAsync(productIds, ct);
        var products = await _db.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var prices = await SalePrices.LoadAsync(_db, _time.GetUtcNow().UtcDateTime, ct);
        var columns = new List<CompareColumn>();
        foreach (var id in ids)
        {
            // Gone between the two reads: leave it out like any hidden product.
            if (!products.TryGetValue(id, out var product))
                continue;
            var configurations = await ProductCatalogService.LoadConfigurationsAsync(_db, product, prices, ct);
            var choices = configurations.SelectMany(c => c.Choices).ToList();
            var reviews = await _reviews.ForProductAsync(id, ct);
            columns.Add(new CompareColumn(
                product.Id,
                product.Name,
                product.Slug,
                product.Category.Name,
                (await ProductCatalogService.ImageUrlsAsync(_db, id, ct)).FirstOrDefault(),
                choices.Select(c => c.Price).Min(),
                choices.Any(c => c.StockQty > 0),
                configurations.Select(c => c.Name).ToList(),
                choices.Select(c => c.Color).OfType<string>().Distinct().ToList(),
                choices.Select(c => c.Region).OfType<string>().Distinct().ToList(),
                reviews.Average,
                reviews.Count));
        }
        return columns;
    }

    public async Task<CompareResult> AddAsync(IReadOnlyList<int> productIds, int productId, CancellationToken ct = default)
    {
        var kept = await VisibleAsync(productIds, ct);
        if (kept.Contains(productId))
            return new CompareResult(CompareOutcome.AlreadyIn, kept);
        if (!await _db.Products.AnyAsync(p => p.Id == productId && p.Status, ct))
            return new CompareResult(CompareOutcome.NotFound, kept);
        if (kept.Count >= MaxItems)
            return new CompareResult(CompareOutcome.Full, kept);
        return new CompareResult(CompareOutcome.Added, [.. kept, productId]);
    }

    // The ids of products still on sale, first-added first, at most MaxItems.
    private async Task<List<int>> VisibleAsync(IReadOnlyList<int> productIds, CancellationToken ct)
    {
        var wanted = productIds.Distinct().ToList();
        var visible = await _db.Products.Where(p => p.Status && wanted.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        return wanted.Where(visible.Contains).Take(MaxItems).ToList();
    }
}
