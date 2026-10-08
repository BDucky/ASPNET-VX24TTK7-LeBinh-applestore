namespace AppleStore.Infrastructure.Payments;

// Use case 17, online part. Cash on delivery needs none of this.
public interface IPaymentService
{
    Task<PayStart> StartAsync(int userId, int orderId, CancellationToken ct = default);

    // Fields from the gateway, from its server call or from the shopper's
    // return; either may arrive first, and either may arrive twice.
    Task<CallbackResult> HandleAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct = default);
}
