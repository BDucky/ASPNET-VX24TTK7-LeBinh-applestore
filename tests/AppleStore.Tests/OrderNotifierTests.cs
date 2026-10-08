using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// The notifier over a real order (placed and shipped by the real services)
// and a fake mail server, which is the edge.
public sealed class OrderNotifierTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly FakeEmailSender _mail = new();
    private readonly ListLogger<OrderNotifier> _log = new();
    private readonly OrderNotifier _sut;
    private readonly int _order;

    public OrderNotifierTests()
    {
        _db = _shop.Context();
        var alice = ShopTestDb.NewUser("alice@example.com");
        var phone = AddVariant(_db, NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m), "IP17", 24_990_000m, stock: 5, config: "iPhone 17 256GB");
        _db.Add(alice);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var cart = new CartService(_db);
        cart.AddAsync(alice.Id, phone.Id, 1).GetAwaiter().GetResult();
        var placed = new CheckoutService(_db, cart, new FixedTime(Now))
            .PlaceOrderAsync(alice.Id, new DeliveryInput("Alice", "0900000000", "1 Street", null, null, null, null), null, 24_990_000m, PaymentMethod.Cod)
            .GetAwaiter().GetResult();
        _order = placed.OrderId!.Value;
        _sut = new OrderNotifier(_db, _mail, _log);
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    [Fact]
    public async Task Placing_emails_the_account_with_the_order_number_total_and_link()
    {
        await _sut.OrderPlacedAsync(_order, "https://store.test/Orders/" + _order);

        var (to, subject, body) = Assert.Single(_mail.Sent);
        Assert.Equal("alice@example.com", to);
        Assert.Equal($"Order #{_order} received", subject);
        Assert.Contains("24.990.000 VNĐ", body);
        Assert.Contains("https://store.test/Orders/" + _order, body);
    }

    [Fact]
    public async Task Shipping_emails_the_carrier_and_tracking_number()
    {
        var orders = new OrderManagementService(_db, new FixedTime(Now));
        await orders.ConfirmAsync(_order);
        await orders.ShipAsync(_order, new ShipmentInput("GHN", "GHN123456"));

        await _sut.OrderShippedAsync(_order, "https://store.test/Track");

        var (_, subject, body) = Assert.Single(_mail.Sent);
        Assert.Equal($"Order #{_order} is on its way", subject);
        Assert.Contains("GHN", body);
        Assert.Contains("GHN123456", body);
        Assert.Contains("https://store.test/Track", body);
    }

    [Fact]
    public async Task A_mail_server_that_cannot_be_reached_is_logged_not_thrown()
    {
        _mail.Fail = true;

        await _sut.OrderPlacedAsync(_order, "https://store.test/x");

        Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains($"#{_order}"));
    }

    // The order is already saved when the notifier runs; a database that
    // fails its read must not turn that into an error page.
    [Fact]
    public async Task A_database_that_fails_the_read_is_logged_not_thrown()
    {
        using var broken = new SqliteInMemoryFixture();
        var log = new ListLogger<OrderNotifier>();
        var notifier = new OrderNotifier(broken.Context, _mail, log);

        await notifier.OrderPlacedAsync(_order, "x");
        await notifier.OrderShippedAsync(_order, "x");

        Assert.Empty(_mail.Sent);
        Assert.Equal(2, log.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains($"#{_order}")));
    }

    [Fact]
    public async Task An_unknown_order_sends_nothing()
    {
        await _sut.OrderPlacedAsync(999_999, "x");
        await _sut.OrderShippedAsync(999_999, "x");

        Assert.Empty(_mail.Sent);
    }
}
