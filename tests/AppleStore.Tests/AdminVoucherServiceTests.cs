using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// AdminVoucherService on SQLite; a voucher an admin makes is checked by
// pricing a real cart with the real CheckoutService.
public sealed class AdminVoucherServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly AdminVoucherService _sut;
    private readonly int _alice;
    private readonly int _phone;
    private readonly int _phoneProduct;

    public AdminVoucherServiceTests()
    {
        _db = _shop.Context();
        var alice = ShopTestDb.NewUser("alice@example.com");
        var product = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m);
        var phone = AddVariant(_db, product, "IP17", 20_000_000m, stock: 5, config: "iPhone 17 256GB");
        _db.Add(alice);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_alice, _phone, _phoneProduct) = (alice.Id, phone.Id, product.Id);
        _sut = new AdminVoucherService(_db, new FixedTime(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private VoucherInput Input(string? code = "SALE10", VoucherDiscountType type = VoucherDiscountType.Percent, decimal value = 10m,
        decimal? min = null, int? limit = 100, bool active = true, int startDays = -1, int endDays = 30, params int[] products) =>
        new(code, type, value, min, Now.UtcDateTime.AddDays(startDays), Now.UtcDateTime.AddDays(endDays), limit, active, products);

    private async Task<CheckoutQuote> QuoteAsync(string code)
    {
        var cart = new CartService(_db);
        if (await cart.CountAsync(_alice) == 0)
            await cart.AddAsync(_alice, _phone, 1);
        return await new CheckoutService(_db, cart, new FixedTime(Now)).QuoteAsync(_alice, code);
    }

    [Fact]
    public async Task A_new_voucher_is_stored_upper_case_and_works_at_checkout()
    {
        var result = await _sut.CreateAsync(Input(code: "  sale10 "));

        Assert.Equal(VoucherAdminOutcome.Done, result.Outcome);
        Assert.Equal("SALE10", (await _sut.GetAsync(result.Id!.Value))!.Code);
        Assert.Equal((VoucherProblem.None, 2_000_000m), ((await QuoteAsync("sale10")).VoucherProblem, (await QuoteAsync("sale10")).Discount));
    }

    [Fact]
    public async Task A_voucher_for_some_products_keeps_its_list()
    {
        var result = await _sut.CreateAsync(Input(products: _phoneProduct));

        Assert.Equal([_phoneProduct], (await _sut.GetAsync(result.Id!.Value))!.ProductIds);
    }

    [Theory]
    [InlineData("missingCode")]
    [InlineData("codeTaken")]
    [InlineData("zeroValue")]
    [InlineData("percentOver100")]
    [InlineData("negativeMinimum")]
    [InlineData("endBeforeStart")]
    [InlineData("zeroLimit")]
    [InlineData("unknownProduct")]
    public async Task A_voucher_is_checked_against_the_business_rule(string scenario)
    {
        await _sut.CreateAsync(Input(code: "TAKEN"));
        var (input, expected) = scenario switch
        {
            "missingCode" => (Input(code: "  "), VoucherAdminOutcome.MissingCode),
            "codeTaken" => (Input(code: "taken"), VoucherAdminOutcome.CodeTaken),
            "zeroValue" => (Input(value: 0m), VoucherAdminOutcome.InvalidValue),
            "percentOver100" => (Input(value: 101m), VoucherAdminOutcome.InvalidPercent),
            "negativeMinimum" => (Input(min: -1m), VoucherAdminOutcome.InvalidMinimum),
            "endBeforeStart" => (Input(startDays: 5, endDays: 1), VoucherAdminOutcome.InvalidWindow),
            "zeroLimit" => (Input(limit: 0), VoucherAdminOutcome.InvalidLimit),
            _ => (Input(products: 999), VoucherAdminOutcome.UnknownProduct),
        };

        Assert.Equal(expected, (await _sut.CreateAsync(input)).Outcome);
        Assert.Equal(1, await _db.Vouchers.CountAsync());
    }

    [Fact]
    public async Task A_fixed_voucher_may_be_more_than_100()
    {
        Assert.Equal(VoucherAdminOutcome.Done, (await _sut.CreateAsync(Input(type: VoucherDiscountType.Fixed, value: 500_000m))).Outcome);
    }

    [Fact]
    public async Task Editing_changes_the_rules_but_never_the_use_count()
    {
        var id = (await _sut.CreateAsync(Input())).Id!.Value;
        await QuoteAsync("SALE10");
        await new CheckoutService(_db, new CartService(_db), new FixedTime(Now))
            .PlaceOrderAsync(_alice, new DeliveryInput("A", "0900000000", "1 Street", null, null, null, null), "SALE10", (await QuoteAsync("SALE10")).Total);
        var seen = (await _sut.GetAsync(id))!;

        var result = await _sut.UpdateAsync(id, Input(code: "SALE20", value: 20m), seen.Version);

        Assert.Equal(VoucherAdminOutcome.Done, result.Outcome);
        var after = (await _sut.GetAsync(id))!;
        Assert.Equal(("SALE20", 20m, 1), (after.Code, after.Value, after.UsedCount));
    }

    [Fact]
    public async Task A_limit_below_the_uses_already_made_is_refused()
    {
        var id = (await _sut.CreateAsync(Input())).Id!.Value;
        _db.Database.ExecuteSql($"UPDATE Vouchers SET UsedCount = 3 WHERE Id = {id}");
        var seen = (await _sut.GetAsync(id))!;

        Assert.Equal(VoucherAdminOutcome.LimitBelowUsed, (await _sut.UpdateAsync(id, Input(limit: 2), seen.Version)).Outcome);
        Assert.Equal(VoucherAdminOutcome.Done, (await _sut.UpdateAsync(id, Input(limit: 3), seen.Version)).Outcome);
    }

    [Fact]
    public async Task An_edit_made_meanwhile_is_not_overwritten()
    {
        var id = (await _sut.CreateAsync(Input())).Id!.Value;
        var seen = (await _sut.GetAsync(id))!.Version;
        await _sut.UpdateAsync(id, Input(value: 15m), seen);

        Assert.Equal(VoucherAdminOutcome.Changed, (await _sut.UpdateAsync(id, Input(value: 30m), seen)).Outcome);
        Assert.Equal(15m, (await _sut.GetAsync(id))!.Value);
    }

    [Fact]
    public async Task Renaming_to_another_vouchers_code_is_refused()
    {
        await _sut.CreateAsync(Input(code: "OTHER"));
        var id = (await _sut.CreateAsync(Input())).Id!.Value;

        Assert.Equal(VoucherAdminOutcome.CodeTaken, (await _sut.UpdateAsync(id, Input(code: "other"), (await _sut.GetAsync(id))!.Version)).Outcome);
    }

    [Fact]
    public async Task Deleting_a_voucher_leaves_orders_with_their_code()
    {
        var id = (await _sut.CreateAsync(Input(products: _phoneProduct))).Id!.Value;
        var placed = await new CheckoutService(_db, new CartService(_db), new FixedTime(Now))
            .PlaceOrderAsync(_alice, new DeliveryInput("A", "0900000000", "1 Street", null, null, null, null), "SALE10", (await QuoteAsync("SALE10")).Total);

        Assert.Equal(VoucherAdminOutcome.Done, (await _sut.DeleteAsync(id)).Outcome);

        Assert.Null(await _sut.GetAsync(id));
        Assert.Equal(VoucherProblem.NotFound, (await QuoteAsync("SALE10")).VoucherProblem);
        await using var db = _shop.Context();
        Assert.Equal("SALE10", (await db.Orders.SingleAsync(o => o.Id == placed.OrderId)).VoucherCode);
        Assert.Equal(VoucherAdminOutcome.NotFound, (await _sut.DeleteAsync(id)).Outcome);
    }

    [Fact]
    public async Task The_list_shows_every_voucher_newest_first()
    {
        await _sut.CreateAsync(Input(code: "FIRST"));
        await _sut.CreateAsync(Input(code: "SECOND"));

        Assert.Equal(["SECOND", "FIRST"], (await _sut.ListAsync()).Select(v => v.Code));
    }
}
