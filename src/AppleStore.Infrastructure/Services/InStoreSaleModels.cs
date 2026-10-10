using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

public sealed record SaleLineInput(int VariantId, int Quantity);

// CustomerEmail: an existing account to put the sale under (it then shows
// in that customer's orders); empty for a walk-in customer.
public sealed record SaleInput(Guid FormKey, IReadOnlyList<SaleLineInput> Lines, string? VoucherCode, PaymentMethod Method,
    string? CustomerName, string? CustomerPhone, string? CustomerEmail);

public enum SaleOutcome
{
    Done,
    // The same form sold before; OrderId is that sale.
    AlreadySold,
    NoLines,
    UnknownVariant,
    // One variant twice on the sale; Sku names it.
    DuplicateSku,
    InvalidQuantity,
    // Off sale, or no price yet; Sku names it.
    NotForSale,
    // Not enough stock now; Sku names it.
    OutOfStock,
    VoucherRefused,
    // The total is not the one the staff member saw (a price or promotion changed).
    TotalChanged,
    // Only cash or bank transfer at the counter.
    InvalidMethod,
    // An email was typed that no account has.
    UnknownCustomer,
}

// Options: the variant's full name (VariantText.Label).
public sealed record SaleQuoteLine(int VariantId, string Sku, string ProductName, string? Options, decimal? UnitPrice, decimal? WasPrice, int Quantity, int StockQty)
{
    public decimal? LineTotal => UnitPrice * Quantity;
}

public sealed record SaleQuote(IReadOnlyList<SaleQuoteLine> Lines, decimal Subtotal, decimal Discount, decimal Total, string? VoucherCode,
    VoucherProblem VoucherProblem, SaleOutcome Problem, string? ProblemSku);

public sealed record SaleResult(SaleOutcome Outcome, int? OrderId = null, string? Sku = null, VoucherProblem VoucherProblem = VoucherProblem.None);

public interface IInStoreSaleService
{
    // Prices the lines the way the shop does online (sale prices, voucher).
    Task<SaleQuote> QuoteAsync(IReadOnlyList<SaleLineInput> lines, string? voucherCode, CancellationToken ct = default);

    // The sale a form key already made, if any.
    Task<int?> OrderForKeyAsync(Guid formKey, CancellationToken ct = default);

    // Sells only at the total the staff member saw.
    Task<SaleResult> SellAsync(SaleInput input, decimal expectedTotal, int staffId, CancellationToken ct = default);
}
