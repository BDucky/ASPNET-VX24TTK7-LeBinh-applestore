using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// Use case 20 on SQLite. Owner's choices (2026-10-10): quantity and unit
// cost per line, supplier typed in, one variant twice on a receipt refused.
// A sale landing during the intake is staged with SqlRace.
public sealed class StockServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly SqlRace _race = new();
    private readonly AppDbContext _db;
    private readonly StockService _sut;
    private readonly int _staff;
    private readonly int _blue;
    private readonly int _black;
    private readonly int _retired;

    public StockServiceTests()
    {
        _db = _shop.Context(_race);
        var staff = ShopTestDb.NewUser("staff@example.com");
        staff.FullName = "Kho Nhân Viên";
        var phone = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m);
        var pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods 4", "airpods-4", 1m);
        var blue = AddVariant(_db, phone, "IP17-BLUE", 24_990_000m, 3, config: "iPhone 17 256GB", color: "Blue", region: "VN/A");
        var black = AddVariant(_db, phone, "IP17-BLACK", 25_490_000m, 0, config: "iPhone 17 256GB", color: "Black", region: "VN/A");
        var retired = AddVariant(_db, pods, "PODS-OLD", 3_490_000m, 1, status: false);
        _db.Add(staff);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_staff, _blue, _black, _retired) = (staff.Id, blue.Id, black.Id, retired.Id);
        _sut = new StockService(_db, new FixedTime(Now));
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

    private int Receipts()
    {
        using var db = _shop.Context();
        return db.StockReceipts.Count();
    }

    private static StockReceiptInput Input(params StockReceiptLineInput[] lines) => new("Công ty FPT Trading", "Lô tháng 11", Guid.NewGuid(), lines);

    [Fact]
    public async Task A_receipt_adds_to_stock_and_records_opening_and_closing()
    {
        var result = await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 10, 21_000_000m), new StockReceiptLineInput(_black, 5, 21_500_000m)), _staff);

        Assert.Equal(StockReceiptOutcome.Done, result.Outcome);
        Assert.Equal((13, 5), (Stock(_blue), Stock(_black)));
        var receipt = (await _sut.ReceiptAsync(result.Id!.Value))!;
        Assert.Equal(("Công ty FPT Trading", "Kho Nhân Viên", Now.UtcDateTime), (receipt.Supplier, receipt.CreatedBy, receipt.CreatedAt));
        var blue = receipt.Lines.Single(l => l.VariantId == _blue);
        Assert.Equal(("IP17-BLUE", 3, 13, "Blue"), (blue.Sku, blue.OpeningStock, blue.ClosingStock, blue.Color));
        Assert.Equal((15, 10 * 21_000_000m + 5 * 21_500_000m), (receipt.TotalQuantity, receipt.TotalCost));
    }

    [Fact]
    public async Task The_same_variant_twice_is_refused_and_nothing_is_received()
    {
        var result = await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 1, 1m), new StockReceiptLineInput(_black, 1, 1m), new StockReceiptLineInput(_blue, 2, 1m)), _staff);

        Assert.Equal((StockReceiptOutcome.DuplicateSku, "IP17-BLUE"), (result.Outcome, result.Sku));
        Assert.Equal((3, 0, 0), (Stock(_blue), Stock(_black), Receipts()));
    }

    [Theory]
    [InlineData(0, "1", StockReceiptOutcome.InvalidQuantity)]
    [InlineData(-2, "1", StockReceiptOutcome.InvalidQuantity)]
    [InlineData(1, "-1", StockReceiptOutcome.InvalidCost)]
    [InlineData(1, "10000000000", StockReceiptOutcome.InvalidCost)]
    public async Task A_line_against_the_rules_is_refused(int quantity, string cost, StockReceiptOutcome expected)
    {
        var result = await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, quantity, decimal.Parse(cost))), _staff);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal((3, 0), (Stock(_blue), Receipts()));
    }

    [Fact]
    public async Task A_free_line_is_allowed()
    {
        Assert.Equal(StockReceiptOutcome.Done, (await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 1, 0m)), _staff)).Outcome);
    }

    [Fact]
    public async Task A_receipt_needs_a_supplier_lines_and_known_variants()
    {
        Assert.Equal(StockReceiptOutcome.MissingSupplier, (await _sut.ReceiveAsync(new("  ", null, Guid.NewGuid(), [new StockReceiptLineInput(_blue, 1, 1m)]), _staff)).Outcome);
        Assert.Equal(StockReceiptOutcome.NoLines, (await _sut.ReceiveAsync(Input(), _staff)).Outcome);
        Assert.Equal(StockReceiptOutcome.UnknownVariant, (await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 1, 1m), new StockReceiptLineInput(999_999, 1, 1m)), _staff)).Outcome);
        Assert.Equal((3, 0), (Stock(_blue), Receipts()));
    }

    // Off sale is not out of the warehouse: goods for it can arrive.
    [Fact]
    public async Task A_variant_off_sale_can_be_received()
    {
        Assert.Equal(StockReceiptOutcome.Done, (await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_retired, 4, 2_000_000m)), _staff)).Outcome);
        Assert.Equal(5, Stock(_retired));
    }

    [Fact]
    public async Task The_same_form_saved_twice_receives_the_goods_once()
    {
        var input = Input(new StockReceiptLineInput(_blue, 10, 1m));

        var first = await _sut.ReceiveAsync(input, _staff);
        var second = await _sut.ReceiveAsync(input, _staff);

        Assert.Equal((StockReceiptOutcome.AlreadySaved, first.Id), (second.Outcome, second.Id));
        Assert.Equal((13, 1), (Stock(_blue), Receipts()));
    }

    // A checkout takes one blue phone just as the intake runs: the stock and
    // the line must still agree (closing = opening + received).
    [Fact]
    public async Task A_sale_during_the_intake_keeps_stock_and_line_in_agreement()
    {
        _race.Arm("UPDATE \"ProductVariants\"", $"UPDATE ProductVariants SET StockQty = StockQty - 1 WHERE Id = {_blue}");

        var result = await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 10, 1m)), _staff);

        Assert.True(_race.Ran);
        var line = (await _sut.ReceiptAsync(result.Id!.Value))!.Lines.Single();
        Assert.Equal(12, Stock(_blue));
        Assert.Equal((2, 12), (line.OpeningStock, line.ClosingStock));
    }

    [Fact]
    public async Task Stock_that_would_overflow_is_refused()
    {
        _db.Database.ExecuteSql($"UPDATE ProductVariants SET StockQty = {int.MaxValue - 1} WHERE Id = {_blue}");

        Assert.Equal(StockReceiptOutcome.InvalidQuantity, (await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 5, 1m)), _staff)).Outcome);
        Assert.Equal(int.MaxValue - 1, Stock(_blue));
    }

    [Fact]
    public async Task Receipts_list_newest_first_with_totals()
    {
        await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 1, 100m)), _staff);
        await _sut.ReceiveAsync(Input(new StockReceiptLineInput(_blue, 2, 100m), new StockReceiptLineInput(_black, 3, 50m)), _staff);

        var list = await _sut.ReceiptsAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal((2, 5, 350m, "Kho Nhân Viên"), (list[0].Lines, list[0].Quantity, list[0].TotalCost, list[0].CreatedBy));
    }

    [Fact]
    public async Task Stock_levels_list_lowest_first_and_search_by_sku_or_name()
    {
        var all = await _sut.LevelsAsync();
        var bySku = await _sut.LevelsAsync("ip17-bl");
        var byName = await _sut.LevelsAsync("airpods");

        Assert.Equal(["IP17-BLACK", "PODS-OLD", "IP17-BLUE"], all.Select(r => r.Sku));
        Assert.Equal(["IP17-BLACK", "IP17-BLUE"], bySku.Select(r => r.Sku));
        var pods = Assert.Single(byName);
        Assert.False(pods.OnSale);
    }
}
