using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// The one table of who may do what to an order, read by the service that
// changes orders and by the pages that show the buttons. Agreed 2026-10-08:
// a customer cancels only while the order waits for confirmation, staff
// until it leaves with the carrier; an online order is confirmed only once
// paid.
public static class OrderTransitions
{
    private static readonly Dictionary<OrderAction, (OrderStatus[] Staff, OrderStatus[] Customer, OrderStatus To)> Rules = new()
    {
        [OrderAction.Confirm] = ([OrderStatus.Pending], [], OrderStatus.Confirmed),
        [OrderAction.Ship] = ([OrderStatus.Confirmed], [], OrderStatus.Shipping),
        [OrderAction.Complete] = ([OrderStatus.Shipping], [], OrderStatus.Completed),
        [OrderAction.Cancel] = ([OrderStatus.Pending, OrderStatus.Confirmed], [OrderStatus.Pending], OrderStatus.Cancelled),
    };

    public static IReadOnlyList<OrderStatus> From(OrderAction action, bool byStaff) =>
        byStaff ? Rules[action].Staff : Rules[action].Customer;

    public static OrderStatus To(OrderAction action) => Rules[action].To;

    public static bool NeedsPayment(OrderAction action, PaymentMethod method, OrderPaymentStatus paymentStatus) =>
        action == OrderAction.Confirm && method != PaymentMethod.Cod && paymentStatus != OrderPaymentStatus.Paid;

    public static IReadOnlyList<OrderAction> Allowed(OrderStatus status, PaymentMethod method, OrderPaymentStatus paymentStatus, bool byStaff) =>
        Enum.GetValues<OrderAction>()
            .Where(a => From(a, byStaff).Contains(status) && !NeedsPayment(a, method, paymentStatus))
            .ToList();
}
