using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

public enum OrderAction
{
    Confirm,
    Ship,
    Complete,
    Cancel,
}

public enum OrderChangeOutcome
{
    Done,
    NotFound,
    // The order is not in a state this action starts from (it may have
    // been changed by someone else meanwhile).
    NotAllowed,
    // An online order is confirmed only once it is paid.
    NeedsPayment,
    MissingTracking,
}

// RefundDue: a paid order was cancelled; the money goes back by hand.
public sealed record OrderChangeResult(OrderChangeOutcome Outcome, bool RefundDue = false);

public sealed record ShipmentInput(string? Carrier, string? TrackingNo);

public sealed record OrderListItem(
    int Id,
    DateTime CreatedAt,
    OrderStatus Status,
    OrderPaymentStatus PaymentStatus,
    PaymentMethod PaymentMethod,
    decimal Total,
    string ReceiverName,
    int ItemCount);

// What anyone with the order number and the receiver's phone may see: no
// address, no prices.
public sealed record TrackingResult(int OrderId, DateTime CreatedAt, OrderStatus Status, string? Carrier, string? TrackingNo, ShipmentStatus? ShipmentStatus);
