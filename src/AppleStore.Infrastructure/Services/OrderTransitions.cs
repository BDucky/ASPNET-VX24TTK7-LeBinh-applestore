using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// The one table of who may do what to an order, read by the service that
// changes orders and by the pages that show the buttons. Agreed 2026-10-08:
// a customer cancels only while the order waits for confirmation, staff
// until it leaves with the carrier; an online order is confirmed only once
// paid.
public static class OrderTransitions
{
    public static IReadOnlyList<OrderStatus> From(OrderAction action, bool byStaff) => throw new NotImplementedException();

    public static OrderStatus To(OrderAction action) => throw new NotImplementedException();

    public static IReadOnlyList<OrderAction> Allowed(OrderStatus status, PaymentMethod method, OrderPaymentStatus paymentStatus, bool byStaff) =>
        throw new NotImplementedException();
}
