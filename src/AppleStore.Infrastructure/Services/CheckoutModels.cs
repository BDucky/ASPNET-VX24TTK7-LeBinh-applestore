using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// Why a voucher code gives no discount; None when it applies.
public enum VoucherProblem
{
    None,
    NotFound,
    Inactive,
    NotStarted,
    Expired,
    UsedUp,
    BelowMinimum,
    NoEligibleProducts,
}

public enum CheckoutProblem
{
    None,
    CartEmpty,
    CartHasProblems,
}

// The order as it would be placed now. A voucher that does not apply leaves
// the discount at 0 and says why; MinOrderAmount is set for BelowMinimum.
public sealed record CheckoutQuote(
    CartView Cart,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingFee,
    decimal Total,
    string? VoucherCode,
    VoucherProblem VoucherProblem,
    CheckoutProblem Problem,
    decimal? MinOrderAmount = null);

public sealed record DeliveryInput(
    string ReceiverName,
    string Phone,
    string AddressLine,
    string? Ward,
    string? District,
    string? City,
    string? Note);

public enum PlaceOrderOutcome
{
    Placed,
    CartEmpty,
    CartHasProblems,
    CartChanged,
    VoucherRefused,
    TotalChanged,
    OutOfStock,
}

// ProductName names the line that ran out (OutOfStock).
public sealed record PlaceOrderResult(
    PlaceOrderOutcome Outcome,
    int? OrderId = null,
    VoucherProblem VoucherProblem = VoucherProblem.None,
    string? ProductName = null);

public sealed record OrderLineSummary(string Name, string? Color, string? Region, decimal Price, int Quantity)
{
    public decimal LineTotal => Price * Quantity;
}

public sealed record OrderSummary(
    int Id,
    DateTime CreatedAt,
    OrderStatus Status,
    OrderPaymentStatus PaymentStatus,
    PaymentMethod PaymentMethod,
    IReadOnlyList<OrderLineSummary> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingFee,
    decimal Total,
    string? VoucherCode,
    string ReceiverName,
    string Phone,
    string AddressLine,
    string? Ward,
    string? District,
    string? City,
    string? Note);
