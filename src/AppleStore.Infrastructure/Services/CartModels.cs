namespace AppleStore.Infrastructure.Services;

public enum CartOutcome
{
    Ok,
    NotFound,
    Unavailable,
    NoPrice,
    OutOfStock,
    InvalidQuantity,
    ExceedsStock,
}

// Available: how many more of the variant the cart may hold (ExceedsStock only).
public sealed record CartResult(CartOutcome Outcome, int Available = 0)
{
    public static readonly CartResult Ok = new(CartOutcome.Ok);
}

// Why a line cannot be bought as it stands; None when it can.
public enum CartLineProblem
{
    None,
    Unavailable,
    NoPrice,
    OutOfStock,
    NotEnoughStock,
}

public sealed record CartLine(
    int ItemId,
    int VariantId,
    int ProductId,
    string ProductName,
    string ProductSlug,
    string ConfigurationName,
    string ConfigurationSlug,
    string? Color,
    string? Region,
    string? ImageUrl,
    decimal? UnitPrice,
    int Quantity,
    int StockQty,
    CartLineProblem Problem,
    // The variant's own price while a promotion lowers UnitPrice.
    decimal? WasUnitPrice = null)
{
    public decimal? LineTotal => UnitPrice * Quantity;
}

public sealed record CartView(IReadOnlyList<CartLine> Lines)
{
    // Only lines that can be bought count towards the subtotal.
    public decimal Subtotal => Lines.Where(l => l.Problem == CartLineProblem.None).Sum(l => l.LineTotal ?? 0);

    public int ItemCount => Lines.Sum(l => l.Quantity);
}
