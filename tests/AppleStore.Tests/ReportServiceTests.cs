using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// ReportService over orders written straight into SQLite with the states the
// rest of the shop produces (paid, waiting, cancelled), so each rule of the
// revenue definition is pinned by a case.
public sealed class ReportServiceTests : IDisposable
{
    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly ReportService _sut;
    private readonly int _user;
    private readonly Product _phone;
    private readonly Product _pods;
    private readonly ProductVariant _phoneVariant;
    private readonly ProductVariant _podsVariant;

    public ReportServiceTests()
    {
        _db = _shop.Context();
        var user = ShopTestDb.NewUser("buyer@example.com");
        _phone = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m);
        _pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods Pro 3", "airpods-pro-3", 1m);
        _phoneVariant = AddVariant(_db, _phone, "P", 20_000_000m, 10);
        _podsVariant = AddVariant(_db, _pods, "A", 5_000_000m, 10);
        _db.Add(user);
        _db.SaveChanges();
        _user = user.Id;
        _sut = new ReportService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    // utc: when the order was placed. Lines: (variant, quantity).
    private void Order(DateTime utc, OrderStatus status, OrderPaymentStatus paid, decimal discount, params (ProductVariant Variant, int Qty)[] lines)
    {
        var subtotal = lines.Sum(l => l.Variant.Price!.Value * l.Qty);
        var order = new Order
        {
            UserId = _user,
            Status = status,
            PaymentStatus = paid,
            Subtotal = subtotal,
            DiscountAmount = discount,
            TotalAmount = subtotal - discount,
            ReceiverName = "R",
            Phone = "0900000000",
            AddressLine = "1 Street",
            CreatedAt = utc,
            UpdatedAt = utc,
        };
        _db.Orders.Add(order);
        foreach (var (variant, qty) in lines)
            _db.OrderItems.Add(new OrderItem { Order = order, ProductId = variant.ProductId, VariantId = variant.Id, Price = variant.Price!.Value, Quantity = qty });
        _db.SaveChanges();
    }

    private static DateTime Utc(int day, int hour) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    private async Task<SalesReport> ReportAsync(int fromDay = 1, int toDay = 30)
    {
        var (report, problem) = await _sut.SalesAsync(new DateOnly(2026, 11, fromDay), new DateOnly(2026, 11, toDay));
        Assert.Equal(ReportProblem.None, problem);
        return report!;
    }

    [Fact]
    public async Task Only_paid_orders_that_were_not_cancelled_make_revenue()
    {
        Order(Utc(3, 2), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_phoneVariant, 1));
        Order(Utc(3, 3), OrderStatus.Pending, OrderPaymentStatus.Unpaid, 0m, (_phoneVariant, 1));
        Order(Utc(3, 4), OrderStatus.Cancelled, OrderPaymentStatus.Paid, 0m, (_phoneVariant, 1));
        Order(Utc(3, 5), OrderStatus.Cancelled, OrderPaymentStatus.Unpaid, 0m, (_podsVariant, 1));

        var report = await ReportAsync();

        Assert.Equal((20_000_000m, 1, 1), (report.Revenue, report.OrdersPaid, report.Items));
        Assert.Equal((4, 1, 2), (report.OrdersPlaced, report.OrdersWaiting, report.OrdersCancelled));
    }

    [Fact]
    public async Task Revenue_is_sales_minus_order_discounts()
    {
        Order(Utc(4, 2), OrderStatus.Confirmed, OrderPaymentStatus.Paid, 2_500_000m, (_phoneVariant, 1), (_podsVariant, 2));

        var report = await ReportAsync();

        Assert.Equal((30_000_000m, 2_500_000m, 27_500_000m, 27_500_000m), (report.Sales, report.Discount, report.Revenue, report.AverageOrder));
    }

    // 18:00 UTC on 1 November is 01:00 on 2 November in Vietnam.
    [Fact]
    public async Task Days_are_the_shops_days_in_vietnam_time()
    {
        Order(Utc(1, 18), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_podsVariant, 1));
        Order(Utc(1, 16), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_podsVariant, 1));

        var report = await ReportAsync();

        Assert.Equal([(new DateOnly(2026, 11, 1), 1), (new DateOnly(2026, 11, 2), 1)], report.Days.Select(d => (d.Date, d.Orders)));
    }

    [Fact]
    public async Task The_range_includes_both_end_days_in_vietnam_time_and_nothing_outside()
    {
        Order(Utc(4, 16), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_podsVariant, 1)); // 4 Nov 23:00 local
        Order(Utc(4, 17), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_podsVariant, 1)); // 5 Nov 00:00 local
        Order(Utc(5, 16), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_podsVariant, 1)); // 5 Nov 23:00 local
        Order(Utc(5, 17), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_podsVariant, 1)); // 6 Nov 00:00 local

        var report = await ReportAsync(5, 5);

        Assert.Equal((2, 10_000_000m), (report.OrdersPaid, report.Revenue));
        Assert.Equal(2, report.OrdersPlaced);
    }

    [Fact]
    public async Task Days_are_in_order_and_products_by_sales_largest_first()
    {
        Order(Utc(7, 2), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_podsVariant, 3));
        Order(Utc(6, 2), OrderStatus.Completed, OrderPaymentStatus.Paid, 0m, (_phoneVariant, 1), (_podsVariant, 1));

        var report = await ReportAsync();

        Assert.Equal([new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 7)], report.Days.Select(d => d.Date));
        Assert.Equal([("AirPods Pro 3", 4, 20_000_000m), ("iPhone 17", 1, 20_000_000m)], report.Products.Select(p => (p.Name, p.Quantity, p.Sales)));
    }

    [Fact]
    public async Task An_empty_range_reports_zeros_not_errors()
    {
        var report = await ReportAsync();

        Assert.Empty(report.Days);
        Assert.Empty(report.Products);
        Assert.Equal((0m, 0m, 0), (report.Revenue, report.AverageOrder, report.OrdersPlaced));
    }

    [Fact]
    public async Task An_end_before_the_start_is_refused()
    {
        var (report, problem) = await _sut.SalesAsync(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 4));

        Assert.Null(report);
        Assert.Equal(ReportProblem.EndBeforeStart, problem);
    }
}
