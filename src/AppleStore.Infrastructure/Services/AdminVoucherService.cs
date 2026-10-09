using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Admin side of BM_VOUCHER_01. The use count belongs to checkout: an edit
// never writes it, and a new limit is saved only if it is still at least the
// uses made at that moment.
public class AdminVoucherService : IAdminVoucherService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public AdminVoucherService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<VoucherAdminRow>> ListAsync(VoucherKind kind = VoucherKind.Code, CancellationToken ct = default) =>
        (await _db.Vouchers.AsNoTracking().OrderByDescending(v => v.Id).ToListAsync(ct))
            .Select(v => Row(v, [])).ToList();

    public async Task<VoucherAdminRow?> GetAsync(int voucherId, VoucherKind kind = VoucherKind.Code, CancellationToken ct = default)
    {
        var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == voucherId, ct);
        if (voucher is null)
            return null;
        var products = await _db.VoucherProducts.Where(vp => vp.VoucherId == voucherId).Select(vp => vp.ProductId).OrderBy(id => id).ToListAsync(ct);
        return Row(voucher, products);
    }

    public async Task<VoucherAdminResult> CreateAsync(VoucherInput input, CancellationToken ct = default)
    {
        if (await CheckAsync(input, null, ct) is { } refused)
            return refused;

        var now = _time.GetUtcNow().UtcDateTime;
        var voucher = new Voucher { UsedCount = 0, CreatedAt = now };
        Apply(voucher, input, now);
        _db.Vouchers.Add(voucher);
        _db.VoucherProducts.AddRange(input.ProductIds.Distinct().Select(p => new VoucherProduct { Voucher = voucher, ProductId = p }));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same code saved a moment earlier (unique index on Code).
            _db.ChangeTracker.Clear();
            return new VoucherAdminResult(VoucherAdminOutcome.CodeTaken);
        }
        return new VoucherAdminResult(VoucherAdminOutcome.Done, voucher.Id);
    }

    public async Task<VoucherAdminResult> UpdateAsync(int voucherId, VoucherInput input, long version, CancellationToken ct = default)
    {
        if (await CheckAsync(input, voucherId, ct) is { } refused)
            return refused;

        var next = RowVersion.Next(version, _time.GetUtcNow().UtcDateTime);
        var code = Code(input.Code)!;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var saved = await _db.Vouchers
            .Where(v => v.Id == voucherId && v.UpdatedAt == new DateTime(version)
                && (input.UsageLimit == null || v.UsedCount <= input.UsageLimit)
                && (v.Code == code || v.UsedCount == 0))
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Code, code)
                .SetProperty(v => v.DiscountType, input.Type)
                .SetProperty(v => v.DiscountValue, input.Value)
                .SetProperty(v => v.MinOrderAmount, input.MinOrderAmount)
                .SetProperty(v => v.StartsAt, input.StartsAt)
                .SetProperty(v => v.EndsAt, input.EndsAt)
                .SetProperty(v => v.UsageLimit, input.UsageLimit)
                .SetProperty(v => v.IsActive, input.IsActive)
                .SetProperty(v => v.UpdatedAt, next), ct);
        if (saved != 1)
        {
            var current = await _db.Vouchers.AsNoTracking().Where(v => v.Id == voucherId).Select(v => new { v.UsedCount, v.UpdatedAt, v.Code }).FirstOrDefaultAsync(ct);
            return new VoucherAdminResult(
                current is null ? VoucherAdminOutcome.NotFound
                : current.UpdatedAt.Ticks != version ? VoucherAdminOutcome.Changed
                : current.Code != code && current.UsedCount > 0 ? VoucherAdminOutcome.CodeLocked
                : VoucherAdminOutcome.LimitBelowUsed);
        }

        await _db.VoucherProducts.Where(vp => vp.VoucherId == voucherId).ExecuteDeleteAsync(ct);
        _db.VoucherProducts.AddRange(input.ProductIds.Distinct().Select(p => new VoucherProduct { VoucherId = voucherId, ProductId = p }));
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new VoucherAdminResult(VoucherAdminOutcome.Done, voucherId);
    }

    public async Task<VoucherAdminResult> DeleteAsync(int voucherId, VoucherKind kind = VoucherKind.Code, CancellationToken ct = default)
    {
        var deleted = await _db.Vouchers.Where(v => v.Id == voucherId).ExecuteDeleteAsync(ct);
        return new VoucherAdminResult(deleted == 1 ? VoucherAdminOutcome.Done : VoucherAdminOutcome.NotFound);
    }

    private async Task<VoucherAdminResult?> CheckAsync(VoucherInput input, int? voucherId, CancellationToken ct)
    {
        var code = Code(input.Code);
        VoucherAdminOutcome? refusal =
            code is null ? VoucherAdminOutcome.MissingCode
            : input.Value <= 0 ? VoucherAdminOutcome.InvalidValue
            : input.Type == VoucherDiscountType.Percent && input.Value > 100 ? VoucherAdminOutcome.InvalidPercent
            : input.MinOrderAmount is < 0 ? VoucherAdminOutcome.InvalidMinimum
            : input.EndsAt <= input.StartsAt ? VoucherAdminOutcome.InvalidWindow
            : input.UsageLimit is < 1 ? VoucherAdminOutcome.InvalidLimit
            : null;
        if (refusal is { } r)
            return new VoucherAdminResult(r);
        if (await _db.Vouchers.AnyAsync(v => v.Code == code && v.Id != voucherId, ct))
            return new VoucherAdminResult(VoucherAdminOutcome.CodeTaken);
        var products = input.ProductIds.Distinct().ToList();
        if (products.Count > 0 && await _db.Products.CountAsync(p => products.Contains(p.Id), ct) != products.Count)
            return new VoucherAdminResult(VoucherAdminOutcome.UnknownProduct);
        return null;
    }

    // Codes are matched without regard to case at checkout, so they are stored upper case.
    private static string? Code(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static void Apply(Voucher voucher, VoucherInput input, DateTime now)
    {
        voucher.Code = Code(input.Code)!;
        voucher.DiscountType = input.Type;
        voucher.DiscountValue = input.Value;
        voucher.MinOrderAmount = input.MinOrderAmount;
        voucher.StartsAt = input.StartsAt;
        voucher.EndsAt = input.EndsAt;
        voucher.UsageLimit = input.UsageLimit;
        voucher.IsActive = input.IsActive;
        voucher.UpdatedAt = now;
    }

    private static VoucherAdminRow Row(Voucher v, IReadOnlyList<int> products) =>
        new(v.Id, v.Code, v.DiscountType, v.DiscountValue, v.MinOrderAmount, v.StartsAt, v.EndsAt, v.UsageLimit, v.UsedCount, v.IsActive, products, v.UpdatedAt.Ticks);
}
