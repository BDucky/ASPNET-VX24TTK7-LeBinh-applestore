using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

public enum PriceBatchMode
{
    // Value is a percent, up or down; the new price is rounded to 1.000 dong.
    Percent,
    // Value is an amount in dong, added (or taken off, when negative) as is.
    Amount,
}

// The products picked, or a whole category; both together mean either.
public sealed record PriceBatchInput(IReadOnlyList<int> ProductIds, int? CategoryId, PriceBatchMode Mode, decimal Value);

public enum PriceBatchOutcome
{
    Done,
    NothingSelected,
    InvalidValue,
    // Some new price would be 0 or less; nothing was changed. Sku names it.
    PriceTooLow,
    // No variant's price would change (none has a price, or rounding kept them).
    NothingToChange,
    // Someone changed one of these prices while the batch ran; nothing was changed.
    Changed,
}

public sealed record PriceBatchResult(PriceBatchOutcome Outcome, int Changed = 0, string? Sku = null);

public sealed record PriceChangeRow(int Id, int ProductId, string ProductName, string Sku, decimal? OldPrice, decimal? NewPrice,
    PriceChangeSource Source, string? ChangedBy, DateTime ChangedAt);

public interface IPriceService
{
    Task<PriceBatchResult> ApplyBatchAsync(PriceBatchInput input, int userId, CancellationToken ct = default);

    // Newest first; one product's, or every change.
    Task<IReadOnlyList<PriceChangeRow>> HistoryAsync(int? productId = null, CancellationToken ct = default);
}
