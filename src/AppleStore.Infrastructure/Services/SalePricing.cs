using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// The price a shopper sees and pays. WasPrice is the variant's own price when
// a promotion lowered it, PromotionName the promotion that did.
public sealed record SalePrice(decimal? Price, decimal? WasPrice = null, string? PromotionName = null);

// Use case 28. The promotions running at one moment, and the one rule that
// turns a variant's own price into its sale price. Every page that shows or
// charges a price reads it from here (catalog, compare, cart, checkout), so
// they cannot disagree. Owner's rules (2026-10-09): an Automatic voucher row
// runs between StartsAt and EndsAt (both ends count, as for a voucher), and
// when several cover a product the largest discount for that price wins.
// Claude's rule: a discount that would leave nothing to pay is not applied.
public sealed class SalePrices
{
    private sealed record Running(int Id, string? Name, VoucherDiscountType Type, decimal Value, HashSet<int>? Products);

    private readonly IReadOnlyList<Running> _running;

    private SalePrices(IReadOnlyList<Running> running) => _running = running;

    public SalePrice For(int productId, decimal? basePrice)
    {
        if (basePrice is not { } price)
            return new SalePrice(null);
        var best = _running
            .Where(p => p.Products is null || p.Products.Contains(productId))
            .Select(p => (p.Id, p.Name, Off: DiscountMath.Amount(p.Type, p.Value, price)))
            .Where(c => c.Off > 0 && c.Off < price)
            .OrderByDescending(c => c.Off).ThenBy(c => c.Id)
            .FirstOrDefault();
        return best.Off == 0 ? new SalePrice(price) : new SalePrice(price - best.Off, price, best.Name);
    }

    public static async Task<SalePrices> LoadAsync(AppDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        var running = await db.Vouchers.AsNoTracking()
            .Where(v => v.Kind == VoucherKind.Automatic && v.IsActive && v.StartsAt <= nowUtc && v.EndsAt >= nowUtc)
            .Select(v => new { v.Id, v.Name, v.DiscountType, v.DiscountValue })
            .ToListAsync(ct);
        if (running.Count == 0)
            return new SalePrices([]);

        var ids = running.Select(p => p.Id).ToList();
        // No rows for a promotion means every product.
        var scopes = (await db.VoucherProducts.Where(vp => ids.Contains(vp.VoucherId)).Select(vp => new { vp.VoucherId, vp.ProductId }).ToListAsync(ct))
            .GroupBy(vp => vp.VoucherId)
            .ToDictionary(g => g.Key, g => g.Select(vp => vp.ProductId).ToHashSet());
        return new SalePrices(running.Select(p => new Running(p.Id, p.Name, p.DiscountType, p.DiscountValue, scopes.GetValueOrDefault(p.Id))).ToList());
    }
}
