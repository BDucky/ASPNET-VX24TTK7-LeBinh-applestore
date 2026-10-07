using System.Data.Common;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// CheckoutService with the real CartService and SQLite schema. A competing
// request is staged by running its SQL just before one of the service's own
// writes (stock, voucher use, emptying the cart), inside the same transaction.
public sealed class CheckoutServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DeliveryInput Delivery = new("Alice Nguyen", "0901234567", "1 Le Loi", "Ben Nghe", "District 1", "Ho Chi Minh City", "Call first");

    private readonly ShopTestDb _shop = new();
    private readonly SqlRace _race = new();
    private readonly AppDbContext _db;
    private readonly CartService _cart;
    private readonly CheckoutService _sut;
    private readonly int _alice;
    private readonly int _bob;
    private readonly int _phone;
    private readonly int _pods;
    private readonly int _cheap;
    private readonly int _podsProduct;

    public CheckoutServiceTests()
    {
        _db = _shop.Context(_race);
        var alice = ShopTestDb.NewUser("alice@example.com");
        var bob = ShopTestDb.NewUser("bob@example.com");
        var iphone = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 20_000_000m);
        var airpods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods Pro 3", "airpods-pro-3", 6_000_000m);
        var phone = AddVariant(_db, iphone, "IP17", 24_990_000m, stock: 5, config: "iPhone 17 256GB", color: "Blue", region: "VN/A");
        var pods = AddVariant(_db, airpods, "APP3", 6_000_000m, stock: 5, config: "AirPods Pro 3");
        var cheap = AddVariant(_db, iphone, "CABLE", 1_005m, stock: 5, config: "iPhone 17 cable");
        _db.AddRange(alice, bob);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_alice, _bob, _phone, _pods, _cheap, _podsProduct) = (alice.Id, bob.Id, phone.Id, pods.Id, cheap.Id, airpods.Id);

        _cart = new CartService(_db);
        _sut = new CheckoutService(_db, _cart, new FixedTime(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private int AddVoucher(string code, VoucherDiscountType type, decimal value, decimal? min = null, int? limit = null, int used = 0,
        bool active = true, DateTime? starts = null, DateTime? ends = null, params int[] onlyProducts)
    {
        using var db = _shop.Context();
        var voucher = new Voucher
        {
            Code = code,
            DiscountType = type,
            DiscountValue = value,
            MinOrderAmount = min,
            UsageLimit = limit,
            UsedCount = used,
            IsActive = active,
            StartsAt = starts ?? Now.UtcDateTime.AddDays(-1),
            EndsAt = ends ?? Now.UtcDateTime.AddDays(30),
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime,
        };
        db.Vouchers.Add(voucher);
        db.SaveChanges();
        db.VoucherProducts.AddRange(onlyProducts.Select(p => new VoucherProduct { VoucherId = voucher.Id, ProductId = p }));
        db.SaveChanges();
        return voucher.Id;
    }

    private void Set(string sql)
    {
        using var db = _shop.Context();
        db.Database.ExecuteSqlRaw(sql);
    }

    private (int Orders, int CartLines, int PhoneStock, int PodsStock) State()
    {
        using var db = _shop.Context();
        return (db.Orders.Count(), db.CartItems.Count(),
            db.ProductVariants.Single(v => v.Id == _phone).StockQty, db.ProductVariants.Single(v => v.Id == _pods).StockQty);
    }

    private int UsedCount(int voucherId)
    {
        using var db = _shop.Context();
        return db.Vouchers.Single(v => v.Id == voucherId).UsedCount;
    }

    private async Task<decimal> TotalAsync(string? code = null) => (await _sut.QuoteAsync(_alice, code)).Total;

    // ---------- Quote ----------

    [Fact]
    public async Task An_empty_cart_cannot_check_out()
    {
        var quote = await _sut.QuoteAsync(_alice, null);

        Assert.Equal(CheckoutProblem.CartEmpty, quote.Problem);
        Assert.Equal(0m, quote.Total);
    }

    [Fact]
    public async Task A_cart_with_a_line_that_cannot_be_bought_cannot_check_out()
    {
        await _cart.AddAsync(_alice, _phone, 1);
        Set($"UPDATE ProductVariants SET Status = 0 WHERE Id = {_phone}");

        Assert.Equal(CheckoutProblem.CartHasProblems, (await _sut.QuoteAsync(_alice, null)).Problem);
    }

    [Fact]
    public async Task Without_a_voucher_the_total_is_the_subtotal_and_shipping_is_free()
    {
        await _cart.AddAsync(_alice, _phone, 2);

        var quote = await _sut.QuoteAsync(_alice, "  ");

        Assert.Equal((49_980_000m, 0m, 0m, 49_980_000m, (string?)null, VoucherProblem.None, CheckoutProblem.None),
            (quote.Subtotal, quote.Discount, quote.ShippingFee, quote.Total, quote.VoucherCode, quote.VoucherProblem, quote.Problem));
    }

    [Fact]
    public async Task A_percent_voucher_rounds_to_whole_dong_half_away_from_zero()
    {
        AddVoucher("TEN", VoucherDiscountType.Percent, 10m);
        await _cart.AddAsync(_alice, _cheap, 1);

        var quote = await _sut.QuoteAsync(_alice, "TEN");

        Assert.Equal((1_005m, 101m, 904m), (quote.Subtotal, quote.Discount, quote.Total));
    }

    [Fact]
    public async Task A_fixed_voucher_never_takes_more_than_the_order()
    {
        AddVoucher("BIG", VoucherDiscountType.Fixed, 50_000_000m);
        await _cart.AddAsync(_alice, _phone, 1);

        var quote = await _sut.QuoteAsync(_alice, "BIG");

        Assert.Equal((24_990_000m, 0m), (quote.Discount, quote.Total));
    }

    [Fact]
    public async Task A_voucher_for_some_products_discounts_only_those_lines()
    {
        AddVoucher("PODS15", VoucherDiscountType.Percent, 15m, onlyProducts: _podsProduct);
        await _cart.AddAsync(_alice, _phone, 1);
        await _cart.AddAsync(_alice, _pods, 1);

        var quote = await _sut.QuoteAsync(_alice, "PODS15");

        Assert.Equal((30_990_000m, 900_000m, 30_090_000m), (quote.Subtotal, quote.Discount, quote.Total));
    }

    [Fact]
    public async Task A_code_matches_without_regard_to_case_or_spaces()
    {
        AddVoucher("WELCOME10", VoucherDiscountType.Percent, 10m);
        await _cart.AddAsync(_alice, _phone, 1);

        var quote = await _sut.QuoteAsync(_alice, "  welcome10 ");

        Assert.Equal(("WELCOME10", VoucherProblem.None, 2_499_000m), (quote.VoucherCode, quote.VoucherProblem, quote.Discount));
    }

    [Theory]
    [InlineData("missing", VoucherProblem.NotFound)]
    [InlineData("inactive", VoucherProblem.Inactive)]
    [InlineData("notStarted", VoucherProblem.NotStarted)]
    [InlineData("expired", VoucherProblem.Expired)]
    [InlineData("usedUp", VoucherProblem.UsedUp)]
    [InlineData("belowMinimum", VoucherProblem.BelowMinimum)]
    [InlineData("noEligible", VoucherProblem.NoEligibleProducts)]
    public async Task A_voucher_that_does_not_apply_says_why_and_takes_nothing_off(string scenario, VoucherProblem expected)
    {
        var at = Now.UtcDateTime;
        _ = scenario switch
        {
            "inactive" => AddVoucher("CODE", VoucherDiscountType.Percent, 10m, active: false),
            "notStarted" => AddVoucher("CODE", VoucherDiscountType.Percent, 10m, starts: at.AddSeconds(1)),
            "expired" => AddVoucher("CODE", VoucherDiscountType.Percent, 10m, ends: at.AddSeconds(-1)),
            "usedUp" => AddVoucher("CODE", VoucherDiscountType.Percent, 10m, limit: 3, used: 3),
            "belowMinimum" => AddVoucher("CODE", VoucherDiscountType.Fixed, 100_000m, min: 25_000_000m),
            "noEligible" => AddVoucher("CODE", VoucherDiscountType.Percent, 10m, onlyProducts: _podsProduct),
            _ => 0,
        };
        await _cart.AddAsync(_alice, _phone, 1);

        var quote = await _sut.QuoteAsync(_alice, "code");

        Assert.Equal((expected, 0m, 24_990_000m), (quote.VoucherProblem, quote.Discount, quote.Total));
        Assert.Equal(scenario == "belowMinimum" ? 25_000_000m : null, quote.MinOrderAmount);
    }

    [Theory]
    [InlineData("startsNow")]
    [InlineData("endsNow")]
    [InlineData("exactlyTheMinimum")]
    [InlineData("oneUseLeft")]
    public async Task A_voucher_on_its_limits_still_applies(string scenario)
    {
        var at = Now.UtcDateTime;
        _ = scenario switch
        {
            "startsNow" => AddVoucher("CODE", VoucherDiscountType.Fixed, 1_000m, starts: at),
            "endsNow" => AddVoucher("CODE", VoucherDiscountType.Fixed, 1_000m, ends: at),
            "exactlyTheMinimum" => AddVoucher("CODE", VoucherDiscountType.Fixed, 1_000m, min: 24_990_000m),
            _ => AddVoucher("CODE", VoucherDiscountType.Fixed, 1_000m, limit: 3, used: 2),
        };
        await _cart.AddAsync(_alice, _phone, 1);

        var quote = await _sut.QuoteAsync(_alice, "CODE");

        Assert.Equal((VoucherProblem.None, 1_000m), (quote.VoucherProblem, quote.Discount));
    }

    // ---------- Place order ----------

    [Fact]
    public async Task Placing_freezes_prices_takes_stock_uses_the_voucher_and_empties_the_cart()
    {
        var voucher = AddVoucher("WELCOME10", VoucherDiscountType.Percent, 10m, limit: 10);
        await _cart.AddAsync(_alice, _phone, 2);
        await _cart.AddAsync(_alice, _pods, 1);

        var result = await _sut.PlaceOrderAsync(_alice, Delivery, "welcome10", await TotalAsync("WELCOME10"));
        Set($"UPDATE ProductVariants SET Price = 1 WHERE Id = {_phone}");

        Assert.Equal(PlaceOrderOutcome.Placed, result.Outcome);
        await using var db = _shop.Context();
        var order = await db.Orders.SingleAsync();
        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal(
            (_alice, OrderStatus.Pending, OrderPaymentStatus.Unpaid, 55_980_000m, 5_598_000m, 0m, 50_382_000m, "WELCOME10", Now.UtcDateTime),
            (order.UserId, order.Status, order.PaymentStatus, order.Subtotal, order.DiscountAmount, order.ShippingFee, order.TotalAmount, order.VoucherCode, order.CreatedAt));
        Assert.Equal(
            ("Alice Nguyen", "0901234567", "1 Le Loi", "Ben Nghe", "District 1", "Ho Chi Minh City", "Call first"),
            (order.ReceiverName, order.Phone, order.AddressLine, order.Ward, order.District, order.City, order.Note));
        var items = await db.OrderItems.OrderBy(i => i.Id).Select(i => new { i.VariantId, i.Price, i.Quantity }).ToListAsync();
        Assert.Equal([(_phone, 24_990_000m, 2), (_pods, 6_000_000m, 1)], items.Select(i => (i.VariantId, i.Price, i.Quantity)));
        var payment = await db.Payments.SingleAsync();
        Assert.Equal((order.Id, PaymentMethod.Cod, PaymentStatus.Pending, 0m), (payment.OrderId, payment.Method, payment.Status, payment.PaidAmount));
        Assert.Equal((1, 0, 3, 4), State());
        Assert.Equal(1, UsedCount(voucher));
    }

    [Fact]
    public async Task Placing_without_a_voucher_stores_no_code()
    {
        await _cart.AddAsync(_alice, _phone, 1);

        var result = await _sut.PlaceOrderAsync(_alice, Delivery, null, 24_990_000m);

        Assert.Equal(PlaceOrderOutcome.Placed, result.Outcome);
        await using var db = _shop.Context();
        Assert.Equal((null, 0m, 24_990_000m), (db.Orders.Single().VoucherCode, db.Orders.Single().DiscountAmount, db.Orders.Single().TotalAmount));
    }

    [Fact]
    public async Task A_total_that_changed_since_the_shopper_saw_it_is_refused_and_nothing_is_written()
    {
        await _cart.AddAsync(_alice, _phone, 1);
        var seen = await TotalAsync();
        Set($"UPDATE ProductVariants SET Price = 25990000 WHERE Id = {_phone}");

        var result = await _sut.PlaceOrderAsync(_alice, Delivery, null, seen);

        Assert.Equal(PlaceOrderOutcome.TotalChanged, result.Outcome);
        Assert.Equal((0, 1, 5, 5), State());
    }

    [Fact]
    public async Task An_empty_cart_or_one_with_problems_places_nothing()
    {
        Assert.Equal(PlaceOrderOutcome.CartEmpty, (await _sut.PlaceOrderAsync(_alice, Delivery, null, 0m)).Outcome);

        await _cart.AddAsync(_alice, _phone, 1);
        Set($"UPDATE ProductVariants SET StockQty = 0 WHERE Id = {_phone}");
        Assert.Equal(PlaceOrderOutcome.CartHasProblems, (await _sut.PlaceOrderAsync(_alice, Delivery, null, 24_990_000m)).Outcome);
        Assert.Equal(0, State().Orders);
    }

    [Fact]
    public async Task A_voucher_that_no_longer_applies_is_refused_and_nothing_is_written()
    {
        var voucher = AddVoucher("OLD", VoucherDiscountType.Percent, 10m, ends: Now.UtcDateTime.AddSeconds(-1));
        await _cart.AddAsync(_alice, _phone, 1);

        var result = await _sut.PlaceOrderAsync(_alice, Delivery, "OLD", 24_990_000m);

        Assert.Equal((PlaceOrderOutcome.VoucherRefused, VoucherProblem.Expired), (result.Outcome, result.VoucherProblem));
        Assert.Equal((0, 1, 5, 5), State());
        Assert.Equal(0, UsedCount(voucher));
    }

    [Fact]
    public async Task Placing_twice_makes_one_order()
    {
        var voucher = AddVoucher("WELCOME10", VoucherDiscountType.Percent, 10m);
        await _cart.AddAsync(_alice, _phone, 1);
        var total = await TotalAsync("WELCOME10");

        var first = await _sut.PlaceOrderAsync(_alice, Delivery, "WELCOME10", total);
        var second = await _sut.PlaceOrderAsync(_alice, Delivery, "WELCOME10", total);

        Assert.Equal((PlaceOrderOutcome.Placed, PlaceOrderOutcome.CartEmpty), (first.Outcome, second.Outcome));
        Assert.Equal((1, 0, 4, 5), State());
        Assert.Equal(1, UsedCount(voucher));
    }

    // ---------- Place order, another request in between ----------

    [Fact]
    public async Task Stock_taken_by_another_order_meanwhile_refuses_and_rolls_back()
    {
        var voucher = AddVoucher("WELCOME10", VoucherDiscountType.Percent, 10m);
        await _cart.AddAsync(_alice, _pods, 1);
        await _cart.AddAsync(_alice, _phone, 2);
        var total = await TotalAsync("WELCOME10");
        _race.Arm("UPDATE \"ProductVariants\"", $"UPDATE ProductVariants SET StockQty = 1 WHERE Id = {_phone}");

        var result = await _sut.PlaceOrderAsync(_alice, Delivery, "WELCOME10", total);

        Assert.True(_race.Ran);
        Assert.Equal((PlaceOrderOutcome.OutOfStock, "iPhone 17 256GB"), (result.Outcome, result.ProductName));
        var (orders, cartLines, _, podsStock) = State();
        Assert.Equal((0, 2, 5), (orders, cartLines, podsStock));
        Assert.Equal(0, UsedCount(voucher));
    }

    [Fact]
    public async Task The_last_use_of_a_voucher_taken_meanwhile_refuses_and_rolls_back()
    {
        var voucher = AddVoucher("LAST", VoucherDiscountType.Fixed, 100_000m, limit: 1);
        await _cart.AddAsync(_alice, _phone, 1);
        var total = await TotalAsync("LAST");
        _race.Arm("UPDATE \"Vouchers\"", $"UPDATE Vouchers SET UsedCount = 1 WHERE Id = {voucher}");

        var result = await _sut.PlaceOrderAsync(_alice, Delivery, "LAST", total);

        Assert.True(_race.Ran);
        Assert.Equal((PlaceOrderOutcome.VoucherRefused, VoucherProblem.UsedUp), (result.Outcome, result.VoucherProblem));
        Assert.Equal((0, 1, 5, 5), State());
    }

    [Fact]
    public async Task A_cart_changed_meanwhile_refuses_and_rolls_back()
    {
        await _cart.AddAsync(_alice, _phone, 1);
        await _cart.AddAsync(_alice, _pods, 1);
        var total = await TotalAsync();
        _race.Arm("DELETE FROM \"CartItems\"", $"DELETE FROM CartItems WHERE VariantId = {_pods}");

        var result = await _sut.PlaceOrderAsync(_alice, Delivery, null, total);

        Assert.True(_race.Ran);
        Assert.Equal(PlaceOrderOutcome.CartChanged, result.Outcome);
        var (orders, _, phoneStock, podsStock) = State();
        Assert.Equal((0, 5, 5), (orders, phoneStock, podsStock));
    }

    // ---------- The placed order ----------

    [Fact]
    public async Task Only_the_owner_can_read_an_order()
    {
        await _cart.AddAsync(_alice, _phone, 1);
        var placed = await _sut.PlaceOrderAsync(_alice, Delivery, null, 24_990_000m);

        var order = await _sut.GetOrderAsync(_alice, placed.OrderId!.Value);

        Assert.NotNull(order);
        var line = Assert.Single(order.Lines);
        Assert.Equal(("iPhone 17 256GB", "Blue", "VN/A", 24_990_000m, 1), (line.Name, line.Color, line.Region, line.Price, line.Quantity));
        Assert.Equal((PaymentMethod.Cod, 24_990_000m, "Alice Nguyen"), (order.PaymentMethod, order.Total, order.ReceiverName));
        Assert.Null(await _sut.GetOrderAsync(_bob, placed.OrderId.Value));
        Assert.Null(await _sut.GetOrderAsync(_alice, 999_999));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Runs another request's SQL once, just before the first command whose
    // text contains the marker, on the same connection and transaction.
    private sealed class SqlRace : DbCommandInterceptor
    {
        private string? _marker;
        private string? _sql;

        public bool Ran { get; private set; }

        public void Arm(string marker, string sql) => (_marker, _sql) = (marker, sql);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Ran && _marker is not null && command.CommandText.Contains(_marker))
            {
                Ran = true;
                await using var other = command.Connection!.CreateCommand();
                other.Transaction = command.Transaction;
                other.CommandText = _sql;
                await other.ExecuteNonQueryAsync(cancellationToken);
            }
            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
