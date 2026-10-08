using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Formatting;
using Microsoft.EntityFrameworkCore;
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

    public async Task OrderPlacedAsync(int orderId, string link, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.Id, o.User.Email, o.User.FullName, o.TotalAmount })
            .FirstOrDefaultAsync(ct);
        if (order is null)
            return;

        await SendAsync(order.Id, order.Email, $"Order #{order.Id} received",
            $"Hello {order.FullName},\n\n" +
            $"Thank you for your order #{order.Id}. The total is {PriceText.Vnd(order.TotalAmount)}.\n" +
            "We will email you again when it leaves with the carrier.\n\n" +
            $"See your order: {link}\n\nApple Store", ct);
    }

    public async Task OrderShippedAsync(int orderId, string link, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.User.Email,
                o.User.FullName,
                Shipment = _db.Shipments.Where(s => s.OrderId == o.Id).OrderByDescending(s => s.Id).Select(s => new { s.Carrier, s.TrackingNo }).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (order is null)
            return;

        await SendAsync(order.Id, order.Email, $"Order #{order.Id} is on its way",
            $"Hello {order.FullName},\n\n" +
            $"Your order #{order.Id} has left with {order.Shipment?.Carrier}, tracking number {order.Shipment?.TrackingNo}.\n\n" +
            $"Follow it: {link}\n\nApple Store", ct);
    }

    private async Task SendAsync(int orderId, string to, string subject, string body, CancellationToken ct)
    {
        try
        {
            await _email.SendAsync(to, subject, body, ct);
        }
        catch (EmailSendException ex)
        {
            _logger.LogWarning(ex, "The email about order #{OrderId} could not be sent to {Email}", orderId, to);
        }
    }
}
