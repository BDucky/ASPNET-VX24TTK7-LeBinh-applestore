using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Admin side of BM_VOUCHER_01, and of automatic promotions (use case 28),
// which share the table (owner's choice). Every call is limited to one kind,
// so the promotions page, open to employees, never reaches a code voucher.
// The use count belongs to checkout: an edit never writes it, and a new
// limit is saved only if it is still at least the uses made at that moment.
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
        (await _db.Vouchers.AsNoTracking().Where(v => v.Kind == kind).OrderByDescending(v => v.Id).ToListAsync(ct))
            .Select(v => Row(v, [])).ToList();

    public async Task<VoucherAdminRow?> GetAsync(int voucherId, VoucherKind kind = VoucherKind.Code, CancellationToken ct = default)
    {
        var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == voucherId && v.Kind == kind, ct);
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
        var code = CodeFor(input);
        var name = NameFor(input);
        var (minimum, limit) = VoucherOnly(input);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var saved = await _db.Vouchers
            .Where(v => v.Id == voucherId && v.Kind == input.Kind && v.UpdatedAt == new DateTime(version)
                && (limit == null || v.UsedCount <= limit)
                && (v.Code == code || v.UsedCount == 0))
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Code, code)
                .SetProperty(v => v.Name, name)
                .SetProperty(v => v.DiscountType, input.Type)
                .SetProperty(v => v.DiscountValue, input.Value)
                .SetProperty(v => v.MinOrderAmount, minimum)
                .SetProperty(v => v.StartsAt, input.StartsAt)
                .SetProperty(v => v.EndsAt, input.EndsAt)
                .SetProperty(v => v.UsageLimit, limit)
                .SetProperty(v => v.IsActive, input.IsActive)
                .SetProperty(v => v.UpdatedAt, next), ct);
        if (saved != 1)
        {
            var current = await _db.Vouchers.AsNoTracking().Where(v => v.Id == voucherId && v.Kind == input.Kind).Select(v => new { v.UsedCount, v.UpdatedAt, v.Code }).FirstOrDefaultAsync(ct);
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
        var deleted = await _db.Vouchers.Where(v => v.Id == voucherId && v.Kind == kind).ExecuteDeleteAsync(ct);
        return new VoucherAdminResult(deleted == 1 ? VoucherAdminOutcome.Done : VoucherAdminOutcome.NotFound);
    }

    private async Task<VoucherAdminResult?> CheckAsync(VoucherInput input, int? voucherId, CancellationToken ct)
    {
        var code = CodeFor(input);
        var percent = input.Type == VoucherDiscountType.Percent;
        // The one place the rules differ by kind. A promotion's percent stays
        // below 100 so it never gives a product away (Claude's rule, the same
        // one SalePrices applies to fixed amounts).
        VoucherAdminOutcome? refusal = input.Kind switch
        {
            VoucherKind.Automatic =>
                NameFor(input) is null ? VoucherAdminOutcome.MissingName
                : input.Value <= 0 ? VoucherAdminOutcome.InvalidValue
                : percent && input.Value >= 100 ? VoucherAdminOutcome.PromotionPercent
                : (VoucherAdminOutcome?)null,
            _ =>
                code is null ? VoucherAdminOutcome.MissingCode
                : input.Value <= 0 ? VoucherAdminOutcome.InvalidValue
                : percent && input.Value > 100 ? VoucherAdminOutcome.InvalidPercent
                : input.MinOrderAmount is < 0 ? VoucherAdminOutcome.InvalidMinimum
                : input.UsageLimit is < 1 ? VoucherAdminOutcome.InvalidLimit
                : null,
        } ?? (input.EndsAt <= input.StartsAt ? VoucherAdminOutcome.InvalidWindow : null);
        if (refusal is { } r)
            return new VoucherAdminResult(r);
        if (code is not null && await _db.Vouchers.AnyAsync(v => v.Code == code && v.Id != voucherId, ct))
            return new VoucherAdminResult(VoucherAdminOutcome.CodeTaken);
        var products = input.ProductIds.Distinct().ToList();
        if (products.Count > 0 && await _db.Products.CountAsync(p => products.Contains(p.Id), ct) != products.Count)
            return new VoucherAdminResult(VoucherAdminOutcome.UnknownProduct);
        return null;
    }

    // Codes are matched without regard to case at checkout, so they are stored
    // upper case. A promotion has no code and only it has a name, whatever was posted.
    private static string? CodeFor(VoucherInput input) =>
        input.Kind == VoucherKind.Automatic || string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim().ToUpperInvariant();

    // A minimum order and a usage limit belong to code vouchers only.
    private static (decimal? Minimum, int? Limit) VoucherOnly(VoucherInput input) =>
        input.Kind == VoucherKind.Automatic ? (null, null) : (input.MinOrderAmount, input.UsageLimit);

    private static string? NameFor(VoucherInput input) =>
        input.Kind == VoucherKind.Automatic && !string.IsNullOrWhiteSpace(input.Name) ? input.Name.Trim() : null;

    private static void Apply(Voucher voucher, VoucherInput input, DateTime now)
    {
        voucher.Kind = input.Kind;
        voucher.Code = CodeFor(input);
        voucher.Name = NameFor(input);
        voucher.DiscountType = input.Type;
        voucher.DiscountValue = input.Value;
        (voucher.MinOrderAmount, voucher.UsageLimit) = VoucherOnly(input);
        voucher.StartsAt = input.StartsAt;
        voucher.EndsAt = input.EndsAt;
        voucher.IsActive = input.IsActive;
        voucher.UpdatedAt = now;
    }

    private static VoucherAdminRow Row(Voucher v, IReadOnlyList<int> products) =>
        new(v.Id, v.Code, v.DiscountType, v.DiscountValue, v.MinOrderAmount, v.StartsAt, v.EndsAt, v.UsageLimit, v.UsedCount, v.IsActive, products, v.UpdatedAt.Ticks,
            v.Kind, v.Name);
}
