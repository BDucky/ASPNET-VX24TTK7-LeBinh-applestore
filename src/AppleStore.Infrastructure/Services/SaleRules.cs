using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// The rules an online checkout and an in-person sale share (use cases 15-16
// and 21), so both price and take stock the same way.
public static class SaleRules
{
    public sealed record VoucherCheck(decimal Discount, VoucherProblem Problem, decimal? Minimum, int? VoucherId);

    // BM_VOUCHER_01. Both ends of the window count as valid; the amount comes
    // from DiscountMath, taken from the lines the voucher applies to.
    // Lines are (product, line total at the sale price).
    public static async Task<VoucherCheck> CheckVoucherAsync(AppDbContext db, DateTime nowUtc, string code,
        IReadOnlyList<(int ProductId, decimal LineTotal)> lines, decimal subtotal, CancellationToken ct)
    {
        var voucher = await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == code && v.Kind == VoucherKind.Code, ct);
        if (voucher is null)
            return new VoucherCheck(0m, VoucherProblem.NotFound, null, null);

        var refusal = !voucher.IsActive ? VoucherProblem.Inactive
            : nowUtc < voucher.StartsAt ? VoucherProblem.NotStarted
            : nowUtc > voucher.EndsAt ? VoucherProblem.Expired
            : voucher.UsageLimit is { } limit && voucher.UsedCount >= limit ? VoucherProblem.UsedUp
            : voucher.MinOrderAmount is { } min && subtotal < min ? VoucherProblem.BelowMinimum
            : VoucherProblem.None;
        if (refusal != VoucherProblem.None)
            return new VoucherCheck(0m, refusal, refusal == VoucherProblem.BelowMinimum ? voucher.MinOrderAmount : null, voucher.Id);

        var scope = await db.VoucherProducts.Where(vp => vp.VoucherId == voucher.Id).Select(vp => vp.ProductId).ToListAsync(ct);
        var eligible = scope.Count == 0 ? subtotal : lines.Where(l => scope.Contains(l.ProductId)).Sum(l => l.LineTotal);
        if (eligible == 0m)
            return new VoucherCheck(0m, VoucherProblem.NoEligibleProducts, null, voucher.Id);

        return new VoucherCheck(DiscountMath.Amount(voucher.DiscountType, voucher.DiscountValue, eligible), VoucherProblem.None, null, voucher.Id);
    }

    // Takes the stock only if there is enough and the variant and product are
    // on sale, in one conditional update; false means nothing was taken.
    public static async Task<bool> TryTakeStockAsync(AppDbContext db, int variantId, int quantity, CancellationToken ct) =>
        await db.ProductVariants
            .Where(v => v.Id == variantId && v.StockQty >= quantity && v.Status && v.Product.Status)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, v => v.StockQty - quantity), ct) == 1;

    // Counts one use, only while the voucher is active and under its limit.
    public static async Task<bool> TryUseVoucherAsync(AppDbContext db, int voucherId, CancellationToken ct) =>
        await db.Vouchers
            .Where(v => v.Id == voucherId && v.IsActive && (v.UsageLimit == null || v.UsedCount < v.UsageLimit))
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1), ct) == 1;
}
