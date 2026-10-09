using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// BM_PRICE_01: batch price changes and the price history. A batch is all or
// nothing in one transaction: each variant is saved only if its price and
// version are still the ones read, and moves its version, so an edit page
// opened before the batch cannot save the old price back. Variants that are
// off sale are changed too (they keep a price for when they return);
// variants with no price ("Contact for price") are left alone (Claude's).
public class PriceService : IPriceService
{
    // Owner's choice 2026-10-09: a percent change lands on a whole 1.000 dong.
    private const decimal RoundTo = 1_000m;

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public PriceService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<PriceBatchResult> ApplyBatchAsync(PriceBatchInput input, int userId, CancellationToken ct = default)
    {
        var products = input.ProductIds.Distinct().ToList();
        if (products.Count == 0 && input.CategoryId is null)
            return new PriceBatchResult(PriceBatchOutcome.NothingSelected);
        if (input.Value == 0 || (input.Mode == PriceBatchMode.Percent && input.Value <= -100))
            return new PriceBatchResult(PriceBatchOutcome.InvalidValue);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var variants = await _db.ProductVariants.AsNoTracking()
            .Where(v => v.Price != null && (products.Contains(v.ProductId) || v.Product.CategoryId == input.CategoryId))
            .OrderBy(v => v.Id)
            .Select(v => new { v.Id, v.SKU, Price = v.Price!.Value, v.UpdatedAt })
            .ToListAsync(ct);
        var changes = variants
            .Select(v => (v.Id, v.SKU, Old: v.Price, New: NewPrice(input, v.Price), v.UpdatedAt))
            .Where(c => c.New != c.Old)
            .ToList();
        if (changes.FirstOrDefault(c => c.New <= 0) is { SKU: not null } tooLow)
            return new PriceBatchResult(PriceBatchOutcome.PriceTooLow, Sku: tooLow.SKU);
        if (changes.Count == 0)
            return new PriceBatchResult(PriceBatchOutcome.NothingToChange);

        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var c in changes)
        {
            var next = RowVersion.Next(c.UpdatedAt.Ticks, now);
            var saved = await _db.ProductVariants
                .Where(v => v.Id == c.Id && v.Price == c.Old && v.UpdatedAt == c.UpdatedAt)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, c.New).SetProperty(v => v.UpdatedAt, next), ct);
            if (saved != 1)
            {
                await tx.RollbackAsync(ct);
                return new PriceBatchResult(PriceBatchOutcome.Changed);
            }
        }
        _db.PriceChanges.AddRange(changes.Select(c => new PriceChange
        {
            VariantId = c.Id,
            OldPrice = c.Old,
            NewPrice = c.New,
            Source = PriceChangeSource.Batch,
            ChangedByUserId = userId,
            ChangedAt = now,
        }));
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new PriceBatchResult(PriceBatchOutcome.Done, changes.Count);
    }

    private static decimal NewPrice(PriceBatchInput input, decimal price) => input.Mode == PriceBatchMode.Percent
        ? Math.Round(price * (100m + input.Value) / 100m / RoundTo, 0, MidpointRounding.AwayFromZero) * RoundTo
        : price + input.Value;

    public async Task<IReadOnlyList<PriceChangeRow>> HistoryAsync(int? productId = null, CancellationToken ct = default) =>
        await _db.PriceChanges.AsNoTracking()
            .Where(c => productId == null || c.Variant.ProductId == productId)
            .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id)
            .Select(c => new PriceChangeRow(c.Id, c.Variant.ProductId, c.Variant.Product.Name, c.Variant.SKU, c.OldPrice, c.NewPrice, c.Source,
                c.ChangedBy == null ? null : c.ChangedBy.FullName, c.ChangedAt))
            .ToListAsync(ct);
}
