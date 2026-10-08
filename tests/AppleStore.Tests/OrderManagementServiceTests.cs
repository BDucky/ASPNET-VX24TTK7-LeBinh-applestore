using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// Orders placed through the real CartService and CheckoutService, then moved
// along by OrderManagementService, on SQLite.
public sealed class OrderManagementServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
    private const decimal Price = 10_000_000m;

    private readonly ShopTestDb _shop = new();
    private readonly SqlRace _race = new();
    private readonly AppDbContext _db;
    private readonly OrderManagementService _sut;
    private readonly int _alice;
    private readonly int _bob;
    private readonly int _phone;
    private readonly int _voucher;

    public OrderManagementServiceTests()
    {
        _db = _shop.Context(_race);
        var alice = ShopTestDb.NewUser("alice@example.com");
        var bob = ShopTestDb.NewUser("bob@example.com");
        var phone = AddVariant(_db, NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m), "IP17", Price, stock: 10, config: "iPhone 17 256GB");
        var voucher = new Voucher
        {
            Code = "TAKE1M",
            DiscountType = VoucherDiscountType.Fixed,
            DiscountValue = 1_000_000m,
            StartsAt = Now.UtcDateTime.AddDays(-1),
            EndsAt = Now.UtcDateTime.AddDays(10),
            IsActive = true,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime,
        };
        _db.AddRange(alice, bob, voucher);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_alice, _bob, _phone, _voucher) = (alice.Id, bob.Id, phone.Id, voucher.Id);
        _sut = new OrderManagementService(_db, new FixedTime(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private async Task<int> PlaceAsync(PaymentMethod method = PaymentMethod.Cod, int quantity = 2, string? voucher = null, int? userId = null, string phone = "0912 345 678")
    {
        var user = userId ?? _alice;
        var cart = new CartService(_db);
        await cart.AddAsync(user, _phone, quantity);
        var checkout = new CheckoutService(_db, cart, new FixedTime(Now));
        var total = (await checkout.QuoteAsync(user, voucher)).Total;
        var placed = await checkout.PlaceOrderAsync(user, new DeliveryInput("Receiver", phone, "1 Street", null, null, null, null), voucher, total, method);
        Assert.Equal(PlaceOrderOutcome.Placed, placed.Outcome);
        return placed.OrderId!.Value;
    }

    private void Run(string sql)
    {
        using var db = _shop.Context();
        db.Database.ExecuteSqlRaw(sql);
    }

    private (OrderStatus Status, OrderPaymentStatus Paid, int Stock, int VoucherUses) State(int orderId)
    {
        using var db = _shop.Context();
        var order = db.Orders.Single(o => o.Id == orderId);
        return (order.Status, order.PaymentStatus, db.ProductVariants.Single(v => v.Id == _phone).StockQty, db.Vouchers.Single(v => v.Id == _voucher).UsedCount);
    }

    private List<(PaymentStatus Status, decimal Paid, DateTime? PaidAt)> Payments(int orderId)
    {
        using var db = _shop.Context();
        return db.Payments.Where(p => p.OrderId == orderId).OrderBy(p => p.Id).AsEnumerable().Select(p => (p.Status, p.PaidAmount, p.PaidAt)).ToList();
    }

    private Shipment? ShipmentOf(int orderId)
    {
        using var db = _shop.Context();
        return db.Shipments.SingleOrDefault(s => s.OrderId == orderId);
    }

    private static readonly ShipmentInput Ghn = new(" GHN ", " GHN123456 ");

    // ---------- The rules table ----------

    [Theory]
    [InlineData(OrderStatus.Pending, PaymentMethod.Cod, OrderPaymentStatus.Unpaid, true, "Confirm,Cancel")]
    [InlineData(OrderStatus.Pending, PaymentMethod.VnPay, OrderPaymentStatus.Unpaid, true, "Cancel")]
    [InlineData(OrderStatus.Pending, PaymentMethod.MoMo, OrderPaymentStatus.Paid, true, "Confirm,Cancel")]
    [InlineData(OrderStatus.Confirmed, PaymentMethod.Cod, OrderPaymentStatus.Unpaid, true, "Ship,Cancel")]
    [InlineData(OrderStatus.Shipping, PaymentMethod.Cod, OrderPaymentStatus.Unpaid, true, "Complete")]
    [InlineData(OrderStatus.Completed, PaymentMethod.Cod, OrderPaymentStatus.Paid, true, "")]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Cod, OrderPaymentStatus.Unpaid, true, "")]
    [InlineData(OrderStatus.Pending, PaymentMethod.Cod, OrderPaymentStatus.Unpaid, false, "Cancel")]
    [InlineData(OrderStatus.Pending, PaymentMethod.VnPay, OrderPaymentStatus.Paid, false, "Cancel")]
    [InlineData(OrderStatus.Confirmed, PaymentMethod.Cod, OrderPaymentStatus.Unpaid, false, "")]
    public void The_rules_table_says_what_each_side_may_do(OrderStatus status, PaymentMethod method, OrderPaymentStatus paid, bool byStaff, string expected)
    {
        Assert.Equal(expected, string.Join(",", OrderTransitions.Allowed(status, method, paid, byStaff)));
    }

    // ---------- Confirm ----------

    [Fact]
    public async Task Staff_confirm_a_cash_order_once()
    {
        var order = await PlaceAsync();

        Assert.Equal(OrderChangeOutcome.Done, (await _sut.ConfirmAsync(order)).Outcome);
        Assert.Equal(OrderChangeOutcome.NotAllowed, (await _sut.ConfirmAsync(order)).Outcome);
        Assert.Equal(OrderStatus.Confirmed, State(order).Status);
    }

    [Fact]
    public async Task An_online_order_is_confirmed_only_once_paid()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);

        Assert.Equal(OrderChangeOutcome.NeedsPayment, (await _sut.ConfirmAsync(order)).Outcome);
        Assert.Equal(OrderStatus.Pending, State(order).Status);

        Run($"UPDATE Orders SET PaymentStatus = 1 WHERE Id = {order}");
        Assert.Equal(OrderChangeOutcome.Done, (await _sut.ConfirmAsync(order)).Outcome);
    }

    [Fact]
    public async Task An_unknown_order_is_not_found()
    {
        Assert.Equal(OrderChangeOutcome.NotFound, (await _sut.ConfirmAsync(999_999)).Outcome);
        Assert.Equal(OrderChangeOutcome.NotFound, (await _sut.CancelAsync(999_999, null)).Outcome);
    }

    // ---------- Ship ----------

    [Theory]
    [InlineData(null, "123")]
    [InlineData("GHN", "  ")]
    [InlineData("", "")]
    public async Task Shipping_needs_a_carrier_and_a_tracking_number(string? carrier, string? tracking)
    {
        var order = await PlaceAsync();
        await _sut.ConfirmAsync(order);

        Assert.Equal(OrderChangeOutcome.MissingTracking, (await _sut.ShipAsync(order, new ShipmentInput(carrier, tracking))).Outcome);
        Assert.Equal(OrderStatus.Confirmed, State(order).Status);
        Assert.Null(ShipmentOf(order));
    }

    [Fact]
    public async Task Only_a_confirmed_order_ships()
    {
        var order = await PlaceAsync();

        Assert.Equal(OrderChangeOutcome.NotAllowed, (await _sut.ShipAsync(order, Ghn)).Outcome);
        Assert.Null(ShipmentOf(order));
    }

    [Fact]
    public async Task Shipping_records_the_carrier_and_tracking_number()
    {
        var order = await PlaceAsync();
        await _sut.ConfirmAsync(order);

        Assert.Equal(OrderChangeOutcome.Done, (await _sut.ShipAsync(order, Ghn)).Outcome);

        var shipment = ShipmentOf(order)!;
        Assert.Equal(("GHN", "GHN123456", ShipmentStatus.InTransit, 0m), (shipment.Carrier, shipment.TrackingNo, shipment.Status, shipment.Fee));
        Assert.Equal(OrderStatus.Shipping, State(order).Status);
    }

    // ---------- Complete ----------

    [Fact]
    public async Task Completing_a_cash_order_records_the_cash_as_paid()
    {
        var order = await PlaceAsync();
        await _sut.ConfirmAsync(order);
        await _sut.ShipAsync(order, Ghn);

        Assert.Equal(OrderChangeOutcome.Done, (await _sut.CompleteAsync(order)).Outcome);

        Assert.Equal((OrderStatus.Completed, OrderPaymentStatus.Paid), (State(order).Status, State(order).Paid));
        Assert.Equal([(PaymentStatus.Success, 2 * Price, (DateTime?)Now.UtcDateTime)], Payments(order));
        Assert.Equal(ShipmentStatus.Delivered, ShipmentOf(order)!.Status);
    }

    [Fact]
    public async Task Completing_an_online_order_leaves_its_payment_as_it_was()
    {
        var order = await PlaceAsync(PaymentMethod.MoMo);
        Run($"UPDATE Orders SET PaymentStatus = 1 WHERE Id = {order}");
        Run($"UPDATE Payments SET Status = 1, PaidAmount = 1 WHERE OrderId = {order}");
        await _sut.ConfirmAsync(order);
        await _sut.ShipAsync(order, Ghn);

        await _sut.CompleteAsync(order);

        Assert.Equal([(PaymentStatus.Success, 1m, (DateTime?)null)], Payments(order));
    }

    [Fact]
    public async Task Only_a_shipping_order_completes()
    {
        var order = await PlaceAsync();
        await _sut.ConfirmAsync(order);

        Assert.Equal(OrderChangeOutcome.NotAllowed, (await _sut.CompleteAsync(order)).Outcome);
        Assert.Equal(OrderPaymentStatus.Unpaid, State(order).Paid);
    }

    // ---------- Cancel ----------

    [Fact]
    public async Task A_customer_cancelling_a_pending_order_gets_the_stock_and_voucher_use_back()
    {
        var order = await PlaceAsync(quantity: 3, voucher: "TAKE1M");
        Assert.Equal((7, 1), (State(order).Stock, State(order).VoucherUses));

        var result = await _sut.CancelAsync(order, _alice);

        Assert.Equal(new OrderChangeResult(OrderChangeOutcome.Done, RefundDue: false), result);
        Assert.Equal((OrderStatus.Cancelled, 10, 0), (State(order).Status, State(order).Stock, State(order).VoucherUses));
        Assert.Equal([PaymentStatus.Failed], Payments(order).Select(p => p.Status));
    }

    // Both read the order as pending; staff confirm it just before the
    // customer's cancel writes. The cancel must find the status moved.
    [Fact]
    public async Task A_confirm_landing_just_before_a_customers_cancel_wins()
    {
        var order = await PlaceAsync(quantity: 3, voucher: "TAKE1M");
        _race.Arm("UPDATE \"Orders\"", $"UPDATE Orders SET Status = 1 WHERE Id = {order}");

        var result = await _sut.CancelAsync(order, _alice);

        Assert.True(_race.Ran);
        Assert.Equal(OrderChangeOutcome.NotAllowed, result.Outcome);
        Assert.Equal((7, 1), (State(order).Stock, State(order).VoucherUses));
        Assert.Equal([PaymentStatus.Pending], Payments(order).Select(p => p.Status));
    }

    [Fact]
    public async Task Cancelling_twice_gives_the_stock_back_once()
    {
        var order = await PlaceAsync(quantity: 3, voucher: "TAKE1M");
        await _sut.CancelAsync(order, null);

        Assert.Equal(OrderChangeOutcome.NotAllowed, (await _sut.CancelAsync(order, null)).Outcome);
        Assert.Equal((10, 0), (State(order).Stock, State(order).VoucherUses));
    }

    [Fact]
    public async Task A_voucher_use_count_never_goes_below_zero()
    {
        var order = await PlaceAsync(voucher: "TAKE1M");
        Run($"UPDATE Vouchers SET UsedCount = 0 WHERE Id = {_voucher}");

        await _sut.CancelAsync(order, null);

        Assert.Equal(0, State(order).VoucherUses);
    }

    [Fact]
    public async Task A_customer_cannot_cancel_once_confirmed_but_staff_can()
    {
        var order = await PlaceAsync();
        await _sut.ConfirmAsync(order);

        Assert.Equal(OrderChangeOutcome.NotAllowed, (await _sut.CancelAsync(order, _alice)).Outcome);
        Assert.Equal(OrderStatus.Confirmed, State(order).Status);
        Assert.Equal(OrderChangeOutcome.Done, (await _sut.CancelAsync(order, null)).Outcome);
    }

    [Fact]
    public async Task Nobody_cancels_an_order_on_its_way()
    {
        var order = await PlaceAsync();
        await _sut.ConfirmAsync(order);
        await _sut.ShipAsync(order, Ghn);

        Assert.Equal(OrderChangeOutcome.NotAllowed, (await _sut.CancelAsync(order, null)).Outcome);
        Assert.Equal((OrderStatus.Shipping, 8), (State(order).Status, State(order).Stock));
    }

    [Fact]
    public async Task A_customer_cannot_cancel_someone_elses_order()
    {
        var order = await PlaceAsync();

        Assert.Equal(OrderChangeOutcome.NotFound, (await _sut.CancelAsync(order, _bob)).Outcome);
        Assert.Equal(OrderStatus.Pending, State(order).Status);
    }

    [Fact]
    public async Task Cancelling_a_paid_order_says_a_refund_is_due()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        Run($"UPDATE Orders SET PaymentStatus = 1 WHERE Id = {order}");
        Run($"UPDATE Payments SET Status = 1 WHERE OrderId = {order}");

        var result = await _sut.CancelAsync(order, _alice);

        Assert.Equal(new OrderChangeResult(OrderChangeOutcome.Done, RefundDue: true), result);
        Assert.Equal([PaymentStatus.Success], Payments(order).Select(p => p.Status));
    }

    // ---------- Lists and tracking ----------

    [Fact]
    public async Task A_customer_sees_their_own_orders_newest_first_and_staff_can_filter()
    {
        var first = await PlaceAsync();
        var other = await PlaceAsync(userId: _bob);
        var second = await PlaceAsync(PaymentMethod.MoMo, quantity: 1);
        await _sut.ConfirmAsync(first);

        var mine = await _sut.ListForUserAsync(_alice);
        var confirmed = await _sut.ListAsync(OrderStatus.Confirmed);
        var all = await _sut.ListAsync(null);

        Assert.Equal([second, first], mine.Select(o => o.Id));
        Assert.Equal((PaymentMethod.MoMo, Price, 1), (mine[0].PaymentMethod, mine[0].Total, mine[0].ItemCount));
        Assert.Equal([first], confirmed.Select(o => o.Id));
        Assert.Equal([second, other, first], all.Select(o => o.Id));
    }

    [Theory]
    [InlineData("0912345678")]
    [InlineData(" 0912.345.678 ")]
    [InlineData("0912-345-678")]
    public async Task Tracking_with_the_receivers_phone_shows_the_status_and_shipment(string phone)
    {
        var order = await PlaceAsync(phone: "0912 345 678");
        await _sut.ConfirmAsync(order);
        await _sut.ShipAsync(order, Ghn);

        var tracked = await _sut.TrackAsync(order, phone);

        Assert.Equal(new TrackingResult(order, Now.UtcDateTime, OrderStatus.Shipping, "GHN", "GHN123456", ShipmentStatus.InTransit), tracked);
    }

    [Theory]
    [InlineData("0999999999")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Tracking_with_another_phone_finds_nothing(string? phone)
    {
        var order = await PlaceAsync(phone: "0912 345 678");

        Assert.Null(await _sut.TrackAsync(order, phone));
        Assert.Null(await _sut.TrackAsync(999_999, "0912345678"));
    }
}
