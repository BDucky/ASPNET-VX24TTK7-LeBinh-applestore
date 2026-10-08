using AppleStore.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace AppleStore.Infrastructure.Services;

public class OrderNotifier : IOrderNotifier
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<OrderNotifier> _logger;

    public OrderNotifier(AppDbContext db, IEmailSender email, ILogger<OrderNotifier> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public Task OrderPlacedAsync(int orderId, string link, CancellationToken ct = default) => throw new NotImplementedException();
    public Task OrderShippedAsync(int orderId, string link, CancellationToken ct = default) => throw new NotImplementedException();
}
