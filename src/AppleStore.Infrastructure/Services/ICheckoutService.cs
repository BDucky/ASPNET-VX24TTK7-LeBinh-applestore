using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// Use cases 15-16: place an order from the cart, with an optional voucher.
public interface ICheckoutService
{
    Task<CheckoutQuote> QuoteAsync(int userId, string? voucherCode, CancellationToken ct = default);

    // expectedTotal is the total the shopper saw; a different total now is refused.
    // An online method leaves the order unpaid until the gateway says otherwise.
    Task<PlaceOrderResult> PlaceOrderAsync(int userId, DeliveryInput delivery, string? voucherCode, decimal expectedTotal,
        PaymentMethod method = PaymentMethod.Cod, CancellationToken ct = default);

    Task<OrderSummary?> GetOrderAsync(int userId, int orderId, CancellationToken ct = default);
}
