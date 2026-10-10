using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// Use case 21 on SQLite. Owner's choices (2026-10-10): an in-person sale is
// an order (channel InStore), paid and completed at once; a walk-in customer
// needs no account; prices already include VAT. Pricing is the online one
// (sale prices, vouchers) through SaleRules; an online checkout taking the
// last unit is staged with SqlRace.
public sealed class InStoreSaleServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly SqlRace _race = new();
    private readonly AppDbContext _db;
    private readonly InStoreSaleService _sut;
    private readonly int _staff;
    private readonly int _alice;
    private readonly int _phone;
    private readonly int _pods;
    private readonly int _retired;
    private readonly int _unpriced;
    private readonly int _phoneProduct;

    public InStoreSaleServiceTests()
    {
        _db = _shop.Context(_race);
        var staff = ShopTestDb.NewUser("staff@example.com");
        staff.FullName = "Thu Ngân";
        var alice = ShopTestDb.NewUser("alice@example.com");
        var iphone = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m);
        var airpods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods 4", "airpods-4", 1m);
        var phone = AddVariant(_db, iphone, "IP17", 24_990_000m, 2, config: "iPhone 17 256GB", color: "Blue");
        var pods = AddVariant(_db, airpods, "PODS", 3_490_000m, 5);
        var retired = AddVariant(_db, airpods, "PODS-OLD", 2_000_000m, 5, status: false);
        var unpriced = AddVariant(_db, airpods, "PODS-ASK", null, 5);
        _db.AddRange(staff, alice);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_staff, _alice, _phone, _pods, _retired, _unpriced, _phoneProduct) = (staff.Id, alice.Id, phone.Id, pods.Id, retired.Id, unpriced.Id, iphone.Id);
        _sut = new InStoreSaleService(_db, new FixedTime(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private int Stock(int variant)
    {
        using var db = _shop.Context();
        return db.ProductVariants.Single(v => v.Id == variant).StockQty;
    }

    private int Orders()
    {
        using var db = _shop.Context();
        return db.Orders.Count();
    }

    private static SaleInput Sale(Guid? key = null, string? voucher = null, PaymentMethod method = PaymentMethod.Cash, string? name = null,
        string? phone = null, string? email = null, params SaleLineInput[] lines) =>
        new(key ?? Guid.NewGuid(), lines, voucher, method, name, phone, email);

    private async Task<SaleResult> SellAsync(SaleInput input) =>
        await _sut.SellAsync(input, (await _sut.QuoteAsync(input.Lines, input.VoucherCode)).Total, _staff);

    [Fact]
    public async Task A_sale_is_a_paid_completed_in_store_order_and_takes_the_stock()
    {
        var result = await SellAsync(Sale(lines: [new(_phone, 1), new(_pods, 2)]));

        Assert.Equal(SaleOutcome.Done, result.Outcome);
        Assert.Equal((1, 3), (Stock(_phone), Stock(_pods)));
        using var db = _shop.Context();
        var order = db.Orders.Single();
        Assert.Equal((OrderChannel.InStore, OrderStatus.Completed, OrderPaymentStatus.Paid, (int?)null, (int?)_staff),
            (order.Channel, order.Status, order.PaymentStatus, order.UserId, order.SoldByUserId));
        Assert.Equal(("Walk-in customer", 31_970_000m, 0m, 31_970_000m), (order.ReceiverName, order.Subtotal, order.ShippingFee, order.TotalAmount));
        var payment = db.Payments.Single();
        Assert.Equal((PaymentMethod.Cash, PaymentStatus.Success, 31_970_000m, (DateTime?)Now.UtcDateTime), (payment.Method, payment.Status, payment.PaidAmount, payment.PaidAt));
        Assert.Equal(2, db.OrderItems.Count());
    }

    // The sale counts in the revenue report like any paid order.
    [Fact]
    public async Task The_revenue_report_counts_the_sale()
    {
        await SellAsync(Sale(lines: [new(_pods, 1)]));

        var (report, _) = await new ReportService(_db).SalesAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1));

        Assert.Equal((3_490_000m, 1), (report!.Revenue, report.OrdersPaid));
    }

    [Fact]
    public async Task A_running_promotion_and_a_voucher_price_the_sale_like_online()
    {
        SalePricingTests.AddPromotion(_db, "Phone week", VoucherDiscountType.Fixed, 990_000m, starts: Now.UtcDateTime.AddDays(-1), ends: Now.UtcDateTime.AddDays(1), onlyProducts: [_phoneProduct]);
        _db.Vouchers.Add(new Voucher { Code = "TENPC", DiscountValue = 10, StartsAt = Now.UtcDateTime.AddDays(-1), EndsAt = Now.UtcDateTime.AddDays(1), IsActive = true, CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime });
        _db.SaveChanges();

        var result = await SellAsync(Sale(voucher: "tenpc", lines: [new(_phone, 1)]));

        Assert.Equal(SaleOutcome.Done, result.Outcome);
        using var db = _shop.Context();
        var order = db.Orders.Single();
        Assert.Equal((24_000_000m, 2_400_000m, 21_600_000m, "TENPC"), (order.Subtotal, order.DiscountAmount, order.TotalAmount, order.VoucherCode));
        Assert.Equal(24_000_000m, db.OrderItems.Single().Price);
        Assert.Equal(1, db.Vouchers.Single(v => v.Code == "TENPC").UsedCount);
    }

    [Fact]
    public async Task A_sale_under_a_customer_email_shows_in_that_customers_orders()
    {
        var result = await SellAsync(Sale(name: "Alice", phone: "0901234567", email: "ALICE@example.com", lines: [new(_pods, 1)]));

        Assert.Equal(SaleOutcome.Done, result.Outcome);
        using var db = _shop.Context();
        var order = db.Orders.Single();
        Assert.Equal((_alice, "Alice", "0901234567"), (order.UserId!.Value, order.ReceiverName, order.Phone));
    }

    [Theory]
    [InlineData("nobody@example.com", PaymentMethod.Cash, SaleOutcome.UnknownCustomer)]
    [InlineData(null, PaymentMethod.Cod, SaleOutcome.InvalidMethod)]
    [InlineData(null, PaymentMethod.VnPay, SaleOutcome.InvalidMethod)]
    public async Task A_sale_with_an_unknown_email_or_an_online_method_is_refused(string? email, PaymentMethod method, SaleOutcome expected)
    {
        var result = await SellAsync(Sale(email: email, method: method, lines: [new(_pods, 1)]));

        Assert.Equal(expected, result.Outcome);
        Assert.Equal((5, 0), (Stock(_pods), Orders()));
    }

    [Fact]
    public async Task Bank_transfer_is_accepted()
    {
        Assert.Equal(SaleOutcome.Done, (await SellAsync(Sale(method: PaymentMethod.BankTransfer, lines: [new(_pods, 1)]))).Outcome);
    }

    [Fact]
    public async Task Not_enough_stock_on_one_line_sells_nothing()
    {
        var result = await SellAsync(Sale(lines: [new(_pods, 2), new(_phone, 3)]));

        Assert.Equal((SaleOutcome.OutOfStock, "IP17"), (result.Outcome, result.Sku));
        Assert.Equal((5, 2, 0), (Stock(_pods), Stock(_phone), Orders()));
    }

    [Theory]
    [InlineData("retired")]
    [InlineData("unpriced")]
    public async Task A_variant_off_sale_or_without_a_price_cannot_be_sold(string which)
    {
        var variant = which == "retired" ? _retired : _unpriced;

        var result = await _sut.SellAsync(Sale(lines: [new(variant, 1)]), 0m, _staff);

        Assert.Equal(SaleOutcome.NotForSale, result.Outcome);
        Assert.Equal(0, Orders());
    }

    [Fact]
    public async Task The_same_variant_twice_is_refused()
    {
        var result = await SellAsync(Sale(lines: [new(_pods, 1), new(_pods, 1)]));

        Assert.Equal((SaleOutcome.DuplicateSku, "PODS"), (result.Outcome, result.Sku));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_quantity_below_one_is_refused(int quantity)
    {
        Assert.Equal(SaleOutcome.InvalidQuantity, (await SellAsync(Sale(lines: [new(_pods, quantity)]))).Outcome);
    }

    [Fact]
    public async Task No_lines_is_refused()
    {
        Assert.Equal(SaleOutcome.NoLines, (await _sut.SellAsync(Sale(), 0m, _staff)).Outcome);
    }

    // The staff member saw one total; a promotion starting before the press
    // must not change what the customer pays without them seeing it.
    [Fact]
    public async Task A_total_changed_since_the_quote_sells_nothing()
    {
        var input = Sale(lines: [new(_pods, 1)]);
        var seen = (await _sut.QuoteAsync(input.Lines, null)).Total;
        SalePricingTests.AddPromotion(_db, "Flash", VoucherDiscountType.Percent, 5m, starts: Now.UtcDateTime.AddDays(-1), ends: Now.UtcDateTime.AddDays(1));

        var result = await _sut.SellAsync(input, seen, _staff);

        Assert.Equal(SaleOutcome.TotalChanged, result.Outcome);
        Assert.Equal((5, 0), (Stock(_pods), Orders()));
    }

    [Fact]
    public async Task The_same_form_sold_twice_sells_once()
    {
        var input = Sale(lines: [new(_pods, 1)]);

        var first = await SellAsync(input);
        var second = await SellAsync(input);

        Assert.Equal((SaleOutcome.AlreadySold, first.OrderId), (second.Outcome, second.OrderId));
        Assert.Equal((4, 1), (Stock(_pods), Orders()));
    }

    // An online checkout takes the last phone just as the counter sells it.
    [Fact]
    public async Task An_online_order_taking_the_last_unit_first_stops_the_sale()
    {
        var input = Sale(lines: [new(_phone, 2)]);
        var seen = (await _sut.QuoteAsync(input.Lines, null)).Total;
        _race.Arm("UPDATE \"ProductVariants\"", $"UPDATE ProductVariants SET StockQty = StockQty - 1 WHERE Id = {_phone}");

        var result = await _sut.SellAsync(input, seen, _staff);

        Assert.True(_race.Ran);
        Assert.Equal((SaleOutcome.OutOfStock, "IP17"), (result.Outcome, result.Sku));
        Assert.Equal(0, Orders());
    }

    [Fact]
    public async Task A_quote_shows_sale_prices_stock_and_the_voucher_problem()
    {
        var quote = await _sut.QuoteAsync([new(_phone, 1), new(_pods, 9)], "NOPE");

        Assert.Equal((24_990_000m, 2), (quote.Lines[0].UnitPrice, quote.Lines[0].StockQty));
        Assert.Equal((SaleOutcome.OutOfStock, "PODS"), (quote.Problem, quote.ProblemSku));
        Assert.Equal(VoucherProblem.NotFound, quote.VoucherProblem);
    }
}
