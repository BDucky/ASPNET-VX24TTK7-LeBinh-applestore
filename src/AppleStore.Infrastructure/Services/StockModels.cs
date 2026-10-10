namespace AppleStore.Infrastructure.Services;

public sealed record StockReceiptLineInput(int VariantId, int Quantity, decimal UnitCost);

// FormKey: the hidden key of the form, so a second press saves nothing new.
public sealed record StockReceiptInput(string? Supplier, string? Note, Guid FormKey, IReadOnlyList<StockReceiptLineInput> Lines);

public enum StockReceiptOutcome
{
    Done,
    // The same form was saved before; Id is that receipt.
    AlreadySaved,
    MissingSupplier,
    NoLines,
    UnknownVariant,
    // One variant twice on the receipt (BM_STOCK_01's duplicate SKU check); Sku names it.
    DuplicateSku,
    InvalidQuantity,
    InvalidCost,
}

public sealed record StockReceiptResult(StockReceiptOutcome Outcome, int? Id = null, string? Sku = null);

public sealed record StockReceiptLineRow(int VariantId, string Sku, string ProductName, string? Configuration, string? Color, string? Region,
    int Quantity, decimal UnitCost, int OpeningStock, int ClosingStock)
{
    public decimal LineCost => Quantity * UnitCost;
}

public sealed record StockReceiptView(int Id, string Supplier, string? Note, string? CreatedBy, DateTime CreatedAt, IReadOnlyList<StockReceiptLineRow> Lines)
{
    public decimal TotalCost => Lines.Sum(l => l.LineCost);
    // long: lines just under int.MaxValue each would overflow an int sum (review 2026-10-10).
    public long TotalQuantity => Lines.Sum(l => (long)l.Quantity);
}

public sealed record StockReceiptSummary(int Id, string Supplier, string? CreatedBy, DateTime CreatedAt, int Lines, long Quantity, decimal TotalCost);

public sealed record StockLevelRow(int VariantId, string Sku, int ProductId, string ProductName, string? Configuration, string? Color, string? Region,
    int StockQty, bool OnSale);

public interface IStockService
{
    Task<StockReceiptResult> ReceiveAsync(StockReceiptInput input, int? userId, CancellationToken ct = default);

    // The receipt a form key already saved, if any.
    Task<int?> ReceiptForKeyAsync(Guid formKey, CancellationToken ct = default);
    Task<IReadOnlyList<StockReceiptSummary>> ReceiptsAsync(CancellationToken ct = default);
    Task<StockReceiptView?> ReceiptAsync(int id, CancellationToken ct = default);

    // Every variant, lowest stock first; search matches the SKU or the product name.
    Task<IReadOnlyList<StockLevelRow>> LevelsAsync(string? search = null, CancellationToken ct = default);
}
