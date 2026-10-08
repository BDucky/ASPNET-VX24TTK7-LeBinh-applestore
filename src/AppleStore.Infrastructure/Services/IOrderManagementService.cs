using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// Use cases 18-19 and 22-24: customers follow their orders, staff move them
// along, either may cancel early. The rules live in OrderTransitions.
public interface IOrderManagementService
{
    Task<IReadOnlyList<OrderListItem>> ListAsync(OrderStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken ct = default);

    Task<OrderChangeResult> ConfirmAsync(int orderId, CancellationToken ct = default);
    Task<OrderChangeResult> ShipAsync(int orderId, ShipmentInput shipment, CancellationToken ct = default);
    Task<OrderChangeResult> CompleteAsync(int orderId, CancellationToken ct = default);

    // customerId set: the customer cancelling their own order; null: staff.
    Task<OrderChangeResult> CancelAsync(int orderId, int? customerId, CancellationToken ct = default);

    // Public lookup by order number and receiver phone (BM_ORDER_TRACK_01).
    Task<TrackingResult?> TrackAsync(int orderId, string? phone, CancellationToken ct = default);
}
