using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Use case 20, BM_STOCK_01: goods received from a supplier, closing stock =
// opening stock + intake. A receipt is all or nothing in one transaction.
// Each line adds to the stock in the database itself (StockQty + n), so a
// sale landing at the same moment is never lost, and the line's closing
// stock is read back after the add: opening = closing - quantity always
// holds, whatever else moved the stock. Owner's choices (2026-10-10):
// quantity and unit cost, supplier typed in, one variant twice refused.
// Claude's: a variant off sale can be received; a free line (cost 0) is
// allowed; costs stay within what decimal(12,2) stores.
public class StockService : IStockService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public StockService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<StockReceiptResult> ReceiveAsync(StockReceiptInput input, int? userId, CancellationToken ct = default)
    {
        var supplier = input.Supplier?.Trim();
        if (string.IsNullOrEmpty(supplier))
            return new StockReceiptResult(StockReceiptOutcome.MissingSupplier);
        if (input.Lines.Count == 0)
            return new StockReceiptResult(StockReceiptOutcome.NoLines);
        if (input.Lines.Any(l => l.Quantity < 1))
            return new StockReceiptResult(StockReceiptOutcome.InvalidQuantity);
        if (input.Lines.Any(l => l.UnitCost is < 0 or > PriceLimits.Max))
            return new StockReceiptResult(StockReceiptOutcome.InvalidCost);

        var ids = input.Lines.Select(l => l.VariantId).Distinct().ToList();
        var skus = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.SKU, ct);
        if (skus.Count != ids.Count)
            return new StockReceiptResult(StockReceiptOutcome.UnknownVariant);
        if (input.Lines.GroupBy(l => l.VariantId).FirstOrDefault(g => g.Count() > 1) is { } twice)
            return new StockReceiptResult(StockReceiptOutcome.DuplicateSku, Sku: skus[twice.Key]);

        if (await SavedWithAsync(input.FormKey, ct) is { } earlier)
            return new StockReceiptResult(StockReceiptOutcome.AlreadySaved, earlier);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var receipt = new StockReceipt
        {
            Supplier = supplier,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            FormKey = input.FormKey,
            CreatedByUserId = userId,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
        };
        // In variant order, so two receipts never lock the same rows in opposite orders.
        foreach (var line in input.Lines.OrderBy(l => l.VariantId))
        {
            var current = await _db.ProductVariants.Where(v => v.Id == line.VariantId).Select(v => v.StockQty).SingleAsync(ct);
            if ((long)current + line.Quantity > int.MaxValue)
            {
                await tx.RollbackAsync(ct);
                return new StockReceiptResult(StockReceiptOutcome.InvalidQuantity);
            }
            await _db.ProductVariants.Where(v => v.Id == line.VariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, v => v.StockQty + line.Quantity), ct);
            var closing = await _db.ProductVariants.Where(v => v.Id == line.VariantId).Select(v => v.StockQty).SingleAsync(ct);
            receipt.Lines.Add(new StockReceiptLine
            {
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                OpeningStock = closing - line.Quantity,
                ClosingStock = closing,
            });
        }
        _db.StockReceipts.Add(receipt);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same form saved a moment earlier (unique FormKey): undo this one.
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            // Anything else is a real failure: rethrown for the caller to log.
            if (await SavedWithAsync(input.FormKey, ct) is { } other)
                return new StockReceiptResult(StockReceiptOutcome.AlreadySaved, other);
            throw;
        }
        await tx.CommitAsync(ct);
        return new StockReceiptResult(StockReceiptOutcome.Done, receipt.Id);
    }

    public Task<int?> ReceiptForKeyAsync(Guid formKey, CancellationToken ct = default) => SavedWithAsync(formKey, ct);

    private Task<int?> SavedWithAsync(Guid formKey, CancellationToken ct) =>
        _db.StockReceipts.Where(r => r.FormKey == formKey).Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StockReceiptSummary>> ReceiptsAsync(CancellationToken ct = default) =>
        (await _db.StockReceipts.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Select(r => new { r.Id, r.Supplier, CreatedBy = r.CreatedBy == null ? null : r.CreatedBy.FullName, r.CreatedAt, Lines = r.Lines.Select(l => new { l.Quantity, l.UnitCost }).ToList() })
            .ToListAsync(ct))
        .Select(r => new StockReceiptSummary(r.Id, r.Supplier, r.CreatedBy, r.CreatedAt, r.Lines.Count, r.Lines.Sum(l => (long)l.Quantity), r.Lines.Sum(l => l.Quantity * l.UnitCost)))
        .ToList();

    public async Task<StockReceiptView?> ReceiptAsync(int id, CancellationToken ct = default)
    {
        var receipt = await _db.StockReceipts.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.Supplier,
                r.Note,
                CreatedBy = r.CreatedBy == null ? null : r.CreatedBy.FullName,
                r.CreatedAt,
                Lines = r.Lines.OrderBy(l => l.Id).Select(l => new { l.VariantId, l.Variant.SKU, ProductName = l.Variant.Product.Name, l.Quantity, l.UnitCost, l.OpeningStock, l.ClosingStock }).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (receipt is null)
            return null;
        var options = await VariantOptionLookup.LoadAsync(_db, receipt.Lines.Select(l => l.VariantId).ToList(), ct);
        return new StockReceiptView(receipt.Id, receipt.Supplier, receipt.Note, receipt.CreatedBy, receipt.CreatedAt,
            receipt.Lines.Select(l => new StockReceiptLineRow(l.VariantId, l.SKU, l.ProductName, options.Get(l.VariantId, "config"),
                options.Get(l.VariantId, "color"), options.Get(l.VariantId, "region"), l.Quantity, l.UnitCost, l.OpeningStock, l.ClosingStock)).ToList());
    }

    public async Task<IReadOnlyList<StockLevelRow>> LevelsAsync(string? search = null, CancellationToken ct = default)
    {
        var variants = _db.ProductVariants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            variants = variants.Where(v => EF.Functions.Like(v.SKU, like) || EF.Functions.Like(v.Product.Name, like));
        }
        var rows = await variants
            .OrderBy(v => v.StockQty).ThenBy(v => v.Product.Name).ThenBy(v => v.Id)
            .Select(v => new { v.Id, v.SKU, v.ProductId, ProductName = v.Product.Name, v.StockQty, OnSale = v.Status && v.Product.Status })
            .ToListAsync(ct);
        var options = await VariantOptionLookup.LoadAsync(_db, rows.Select(r => r.Id).ToList(), ct);
        return rows.Select(r => new StockLevelRow(r.Id, r.SKU, r.ProductId, r.ProductName, options.Get(r.Id, "config"),
            options.Get(r.Id, "color"), options.Get(r.Id, "region"), r.StockQty, r.OnSale)).ToList();
    }
}
