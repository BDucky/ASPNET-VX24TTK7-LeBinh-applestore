using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Use cases 33-34. Orders are stored in UTC; the report speaks in the shop's
// days. Vietnam has no daylight saving, so a fixed UTC+7 is exact.
public class ReportService : IReportService
{
    public static readonly TimeSpan ShopOffset = TimeSpan.FromHours(7);

    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(SalesReport? Report, ReportProblem Problem)> SalesAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
            return (null, ReportProblem.EndBeforeStart);

        var startUtc = from.ToDateTime(TimeOnly.MinValue) - ShopOffset;
        var endUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue) - ShopOffset;
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
            .Select(o => new { o.Id, o.CreatedAt, o.Status, o.PaymentStatus, o.Subtotal, o.DiscountAmount })
            .ToListAsync(ct);

        bool Counts(OrderStatus status, OrderPaymentStatus paid) => paid == OrderPaymentStatus.Paid && status != OrderStatus.Cancelled;
        var paidOrders = orders.Where(o => Counts(o.Status, o.PaymentStatus)).ToList();
        var paidIds = paidOrders.Select(o => o.Id).ToList();
        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => paidIds.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.ProductId, ProductName = i.Product.Name, i.Quantity, i.Price })
            .ToListAsync(ct);
        var itemsByOrder = lines.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var days = paidOrders
            .GroupBy(o => DateOnly.FromDateTime(o.CreatedAt + ShopOffset))
            .OrderBy(g => g.Key)
            .Select(g => new ReportDay(g.Key, g.Count(), g.Sum(o => itemsByOrder.GetValueOrDefault(o.Id)), g.Sum(o => o.Subtotal), g.Sum(o => o.DiscountAmount)))
            .ToList();
        var products = lines
            .GroupBy(l => (l.ProductId, l.ProductName))
            .Select(g => new ReportProduct(g.Key.ProductId, g.Key.ProductName, g.Sum(l => l.Quantity), g.Sum(l => l.Price * l.Quantity)))
            .OrderByDescending(p => p.Sales).ThenBy(p => p.Name)
            .ToList();

        return (new SalesReport(from, to, days, products,
            OrdersPlaced: orders.Count,
            OrdersPaid: paidOrders.Count,
            OrdersWaiting: orders.Count(o => o.Status != OrderStatus.Cancelled && o.PaymentStatus != OrderPaymentStatus.Paid),
            OrdersCancelled: orders.Count(o => o.Status == OrderStatus.Cancelled)), ReportProblem.None);
    }
}
