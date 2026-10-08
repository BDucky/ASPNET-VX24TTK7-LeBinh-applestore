using AppleStore.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace AppleStore.Infrastructure.Payments;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentGateway? _gateway;

    // No gateway registered means only cash on delivery is offered.
    public PaymentService(AppDbContext db, TimeProvider time, ILogger<PaymentService> logger, IPaymentGateway? gateway = null)
    {
        _db = db;
        _time = time;
        _logger = logger;
        _gateway = gateway;
    }

    public Task<PayStart> StartAsync(int userId, int orderId, CancellationToken ct = default) => throw new NotImplementedException();

    public Task<CallbackResult> HandleAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct = default) => throw new NotImplementedException();
}
