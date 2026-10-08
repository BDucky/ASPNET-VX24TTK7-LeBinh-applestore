using System.Globalization;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Payments;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// PaymentService with the real CheckoutService placing the order, the real
// simulated gateway signing results, and SQLite.
public sealed class PaymentServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 30, 0, TimeSpan.Zero);
    private const decimal Total = 24_990_000m;

    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly SimulatedPaymentGateway _gateway = new();
    private readonly PaymentService _sut;
    private readonly int _alice;
    private readonly int _bob;
    private readonly int _phone;

    public PaymentServiceTests()
    {
        _db = _shop.Context();
        var alice = ShopTestDb.NewUser("alice@example.com");
        var bob = ShopTestDb.NewUser("bob@example.com");
        var phone = AddVariant(_db, NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m), "IP17", Total, stock: 9, config: "iPhone 17 256GB");
        _db.AddRange(alice, bob);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_alice, _bob, _phone) = (alice.Id, bob.Id, phone.Id);
        _sut = new PaymentService(_db, new FixedTime(Now), NullLogger<PaymentService>.Instance, _gateway);
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private async Task<int> PlaceAsync(PaymentMethod method, int? userId = null)
    {
        var user = userId ?? _alice;
        var cart = new CartService(_db);
        await cart.AddAsync(user, _phone, 1);
        var checkout = new CheckoutService(_db, cart, new FixedTime(Now));
        var placed = await checkout.PlaceOrderAsync(user, new DeliveryInput("A", "0900000000", "1 Street", null, null, null, null), null, Total, method);
        Assert.Equal(PlaceOrderOutcome.Placed, placed.Outcome);
        return placed.OrderId!.Value;
    }

    private static Dictionary<string, string> Query(string url) =>
        url[(url.IndexOf('?') + 1)..].Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));

    private async Task<int> PaymentIdAsync(int orderId) =>
        int.Parse(Query((await _sut.StartAsync(_alice, orderId)).RedirectUrl!)["paymentId"]);

    private Dictionary<string, string> Result(int paymentId, string code, decimal amount = Total, string txn = "SIM-1") =>
        _gateway.Sign(new Dictionary<string, string>
        {
            ["paymentId"] = paymentId.ToString(CultureInfo.InvariantCulture),
            ["amount"] = amount.ToString("0.##", CultureInfo.InvariantCulture),
            ["result"] = code,
            ["txnId"] = txn,
        });

    private (OrderPaymentStatus Order, List<(PaymentMethod Method, PaymentStatus Status, string? Txn, decimal Paid)> Payments) State(int orderId)
    {
        using var db = _shop.Context();
        return (db.Orders.Single(o => o.Id == orderId).PaymentStatus,
            db.Payments.Where(p => p.OrderId == orderId).OrderBy(p => p.Id).AsEnumerable()
                .Select(p => (p.Method, p.Status, p.TxnId, p.PaidAmount)).ToList());
    }

    // ---------- The gateway ----------

    [Fact]
    public void The_gateway_url_carries_the_attempt_signed()
    {
        var url = _gateway.CreatePaymentUrl(new PaymentRequest(7, 3, 1_500_000m, PaymentMethod.MoMo));
        var fields = Query(url);

        Assert.StartsWith(SimulatedPaymentGateway.Path + "?", url);
        Assert.Equal(("7", "3", "1500000", "MoMo"), (fields["paymentId"], fields["orderId"], fields["amount"], fields["method"]));
        Assert.True(_gateway.Verify(fields));

        fields["amount"] = "1";
        Assert.False(_gateway.Verify(fields));
    }

    [Fact]
    public void Each_app_start_signs_with_its_own_key()
    {
        var fields = Result(1, PaymentResultCode.Success);

        Assert.False(new SimulatedPaymentGateway().Verify(fields));
    }

    // ---------- Starting a payment ----------

    [Fact]
    public async Task Placing_with_an_online_method_records_it_and_leaves_the_order_unpaid()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);

        var (paid, payments) = State(order);
        Assert.Equal(OrderPaymentStatus.Unpaid, paid);
        Assert.Equal([(PaymentMethod.VnPay, PaymentStatus.Pending, (string?)null, 0m)], payments);
    }

    [Fact]
    public async Task Starting_sends_the_shopper_to_the_gateway_for_the_pending_attempt()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);

        var start = await _sut.StartAsync(_alice, order);

        Assert.Equal(PayStartOutcome.Ready, start.Outcome);
        var fields = Query(start.RedirectUrl!);
        Assert.Equal((order.ToString(), "24990000", "VnPay"), (fields["orderId"], fields["amount"], fields["method"]));
        Assert.Single(State(order).Payments);
    }

    [Fact]
    public async Task Another_users_order_cash_on_delivery_and_no_gateway_cannot_start()
    {
        var online = await PlaceAsync(PaymentMethod.VnPay);
        var cod = await PlaceAsync(PaymentMethod.Cod);
        var withoutGateway = new PaymentService(_db, new FixedTime(Now), NullLogger<PaymentService>.Instance);

        Assert.Equal(PayStartOutcome.NotFound, (await _sut.StartAsync(_bob, online)).Outcome);
        Assert.Equal(PayStartOutcome.NotFound, (await _sut.StartAsync(_alice, 999_999)).Outcome);
        Assert.Equal(PayStartOutcome.NotOnline, (await _sut.StartAsync(_alice, cod)).Outcome);
        Assert.Equal(PayStartOutcome.Unavailable, (await withoutGateway.StartAsync(_alice, online)).Outcome);
    }

    // ---------- The gateway's answer ----------

    [Fact]
    public async Task A_successful_answer_marks_the_attempt_and_the_order_paid()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        var payment = await PaymentIdAsync(order);

        var result = await _sut.HandleAsync(Result(payment, PaymentResultCode.Success, txn: "SIM-42"));

        Assert.Equal(new CallbackResult(CallbackOutcome.Paid, order), result);
        var (paid, payments) = State(order);
        Assert.Equal(OrderPaymentStatus.Paid, paid);
        Assert.Equal([(PaymentMethod.VnPay, PaymentStatus.Success, (string?)"SIM-42", Total)], payments);
        await using var db = _shop.Context();
        var row = await db.Payments.SingleAsync(p => p.Id == payment);
        Assert.Equal(Now.UtcDateTime, row.PaidAt);
        Assert.Contains("SIM-42", row.ProviderRaw);
    }

    [Fact]
    public async Task The_same_answer_twice_changes_nothing_the_second_time()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        var answer = Result(await PaymentIdAsync(order), PaymentResultCode.Success);

        await _sut.HandleAsync(answer);
        var second = await _sut.HandleAsync(answer);

        Assert.Equal(new CallbackResult(CallbackOutcome.AlreadyPaid, order), second);
        Assert.Single(State(order).Payments);
    }

    [Fact]
    public async Task A_failure_landing_after_the_success_does_not_undo_it()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        var payment = await PaymentIdAsync(order);
        await _sut.HandleAsync(Result(payment, PaymentResultCode.Success));

        var late = await _sut.HandleAsync(Result(payment, PaymentResultCode.Failed));

        Assert.Equal(CallbackOutcome.AlreadyPaid, late.Outcome);
        Assert.Equal(OrderPaymentStatus.Paid, State(order).Order);
        Assert.Equal(PaymentStatus.Success, State(order).Payments[0].Status);
    }

    // The gateway's success is the one that took the money, so it stands even
    // when a failure for the same attempt landed first.
    [Fact]
    public async Task A_success_landing_after_a_failure_of_the_same_attempt_still_pays()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        var payment = await PaymentIdAsync(order);
        await _sut.HandleAsync(Result(payment, PaymentResultCode.Failed));

        var late = await _sut.HandleAsync(Result(payment, PaymentResultCode.Success, txn: "SIM-LATE"));

        Assert.Equal(new CallbackResult(CallbackOutcome.Paid, order), late);
        var (paid, payments) = State(order);
        Assert.Equal(OrderPaymentStatus.Paid, paid);
        Assert.Equal([(PaymentMethod.VnPay, PaymentStatus.Success, (string?)"SIM-LATE", Total)], payments);
    }

    [Fact]
    public async Task A_tampered_answer_is_rejected_and_changes_nothing()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        var answer = Result(await PaymentIdAsync(order), PaymentResultCode.Failed);
        answer["result"] = PaymentResultCode.Success;

        Assert.Equal(CallbackOutcome.Rejected, (await _sut.HandleAsync(answer)).Outcome);
        Assert.Equal((OrderPaymentStatus.Unpaid, PaymentStatus.Pending), (State(order).Order, State(order).Payments[0].Status));
    }

    [Theory]
    [InlineData("wrongAmount")]
    [InlineData("unknownPayment")]
    [InlineData("cashOnDelivery")]
    [InlineData("missingField")]
    public async Task A_signed_answer_that_does_not_match_an_online_attempt_is_rejected(string scenario)
    {
        var online = await PlaceAsync(PaymentMethod.VnPay);
        var cod = await PlaceAsync(PaymentMethod.Cod);
        var onlinePayment = await PaymentIdAsync(online);
        await using var db = _shop.Context();
        var codPayment = await db.Payments.Where(p => p.OrderId == cod).Select(p => p.Id).SingleAsync();
        var answer = scenario switch
        {
            "wrongAmount" => Result(onlinePayment, PaymentResultCode.Success, amount: 1m),
            "unknownPayment" => Result(999_999, PaymentResultCode.Success),
            "cashOnDelivery" => Result(codPayment, PaymentResultCode.Success),
            _ => _gateway.Sign(new Dictionary<string, string> { ["paymentId"] = onlinePayment.ToString(), ["result"] = PaymentResultCode.Success }),
        };

        Assert.Equal(CallbackOutcome.Rejected, (await _sut.HandleAsync(answer)).Outcome);
        Assert.Equal(OrderPaymentStatus.Unpaid, State(online).Order);
        Assert.Equal(OrderPaymentStatus.Unpaid, State(cod).Order);
    }

    [Theory]
    [InlineData(PaymentResultCode.Failed, CallbackOutcome.Failed)]
    [InlineData(PaymentResultCode.Cancelled, CallbackOutcome.Cancelled)]
    public async Task A_failed_or_cancelled_attempt_can_be_tried_again_and_then_paid(string code, CallbackOutcome outcome)
    {
        var order = await PlaceAsync(PaymentMethod.MoMo);
        var first = await PaymentIdAsync(order);

        Assert.Equal(new CallbackResult(outcome, order), await _sut.HandleAsync(Result(first, code)));
        var second = await PaymentIdAsync(order);
        Assert.NotEqual(first, second);
        Assert.Equal(CallbackOutcome.Paid, (await _sut.HandleAsync(Result(second, PaymentResultCode.Success, txn: "SIM-2"))).Outcome);

        var (paid, payments) = State(order);
        Assert.Equal(OrderPaymentStatus.Paid, paid);
        Assert.Equal([(PaymentMethod.MoMo, PaymentStatus.Failed, (string?)null, 0m), (PaymentMethod.MoMo, PaymentStatus.Success, (string?)"SIM-2", Total)], payments);
    }

    [Fact]
    public async Task Starting_again_while_an_attempt_is_open_reuses_it()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);

        Assert.Equal(await PaymentIdAsync(order), await PaymentIdAsync(order));
        Assert.Single(State(order).Payments);
    }

    [Fact]
    public async Task A_paid_order_cannot_be_started_again()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        await _sut.HandleAsync(Result(await PaymentIdAsync(order), PaymentResultCode.Success));

        Assert.Equal(PayStartOutcome.AlreadyPaid, (await _sut.StartAsync(_alice, order)).Outcome);
    }

    // Two open attempts both paid at the gateway: the second payment is real
    // money, so it is recorded and reported, never dropped.
    [Fact]
    public async Task A_second_successful_attempt_on_a_paid_order_is_recorded_as_paid_twice()
    {
        var order = await PlaceAsync(PaymentMethod.VnPay);
        var first = await PaymentIdAsync(order);
        await using (var db = _shop.Context())
        {
            db.Payments.Add(new Payment { OrderId = order, Method = PaymentMethod.VnPay, Status = PaymentStatus.Pending, CreatedAt = Now.UtcDateTime });
            await db.SaveChangesAsync();
        }
        var second = State(order).Payments.Count == 2 ? await LastPaymentIdAsync(order) : 0;

        await _sut.HandleAsync(Result(first, PaymentResultCode.Success, txn: "SIM-A"));
        var result = await _sut.HandleAsync(Result(second, PaymentResultCode.Success, txn: "SIM-B"));

        Assert.Equal(new CallbackResult(CallbackOutcome.PaidTwice, order), result);
        Assert.Equal([PaymentStatus.Success, PaymentStatus.Success], State(order).Payments.Select(p => p.Status));
        Assert.Equal(OrderPaymentStatus.Paid, State(order).Order);
    }

    private async Task<int> LastPaymentIdAsync(int orderId)
    {
        await using var db = _shop.Context();
        return await db.Payments.Where(p => p.OrderId == orderId).MaxAsync(p => p.Id);
    }

}
