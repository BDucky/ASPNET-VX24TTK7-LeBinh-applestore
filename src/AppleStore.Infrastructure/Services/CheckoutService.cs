using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class CheckoutService : ICheckoutService
{
    private readonly AppDbContext _db;
    private readonly ICartService _cart;
    private readonly TimeProvider _time;

    public CheckoutService(AppDbContext db, ICartService cart, TimeProvider time)
    {
        _db = db;
        _cart = cart;
        _time = time;
    }

    public Task<CheckoutQuote> QuoteAsync(int userId, string? voucherCode, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PlaceOrderResult> PlaceOrderAsync(int userId, DeliveryInput delivery, string? voucherCode, decimal expectedTotal, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OrderSummary?> GetOrderAsync(int userId, int orderId, CancellationToken ct = default) => throw new NotImplementedException();
}
