namespace AppleStore.Infrastructure.Services;

// Emails about an order (use case 37, BM_ORDER_TRACK_01). Never throws: an
// email that cannot be sent is logged, and the order goes on regardless.
// link: where the shopper can see the order, built by the caller.
public interface IOrderNotifier
{
    Task OrderPlacedAsync(int orderId, string link, CancellationToken ct = default);
    Task OrderShippedAsync(int orderId, string link, CancellationToken ct = default);
}
