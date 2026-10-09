using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// BM_PRICE_01 on SQLite: batch price changes and the history they write.
// Owner's choices (2026-10-09): batch by products or a category, percent or
// amount, a percent result rounded to 1.000 dong, all or nothing. A competing
// edit is staged with SqlRace just before the batch's own update.
public sealed class PriceServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly SqlRace _race = new();
    private readonly AppDbContext _db;
    private readonly PriceService _sut;
    private readonly int _staff;
    private readonly int _iphoneCategory;
    private readonly int _phone;
    private readonly int _pods;
    private readonly int _blue;
    private readonly int _black;
    private readonly int _retired;
    private readonly int _contact;
    private readonly int _case;

    public PriceServiceTests()
    {
        _db = _shop.Context(_race);
        var staff = ShopTestDb.NewUser("staff@example.com");
        staff.FullName = "Staff Member";
        var iphone = NewCategory("iPhone", "iphone");
        var phone = NewProduct(iphone, "iPhone 17", "iphone-17", 1m);
        var phoneCase = NewProduct(iphone, "iPhone 17 Case", "iphone-17-case", 1m);
        var pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods 4", "airpods-4", 1m);
        var blue = AddVariant(_db, phone, "BLUE", 24_990_000m, 3);
        var black = AddVariant(_db, phone, "BLACK", 25_490_000m, 3);
        var retired = AddVariant(_db, phone, "OLD", 19_990_000m, 0, status: false);
        var contact = AddVariant(_db, phone, "ASK", null, 1);
        var caseVariant = AddVariant(_db, phoneCase, "CASE", 990_000m, 9);
        AddVariant(_db, pods, "PODS", 3_490_000m, 4);
        _db.Add(staff);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_staff, _iphoneCategory, _phone, _pods) = (staff.Id, iphone.Id, phone.Id, pods.Id);
        (_blue, _black, _retired, _contact, _case) = (blue.Id, black.Id, retired.Id, contact.Id, caseVariant.Id);
        _sut = new PriceService(_db, new FixedTime(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private Dictionary<string, decimal?> Prices()
    {
        using var db = _shop.Context();
        return db.ProductVariants.ToDictionary(v => v.SKU, v => v.Price);
    }

    private List<PriceChange> Log()
    {
        using var db = _shop.Context();
        return db.PriceChanges.OrderBy(c => c.Id).ToList();
    }

    private Task<PriceBatchResult> BatchAsync(PriceBatchMode mode, decimal value, int? category = null, params int[] products) =>
        _sut.ApplyBatchAsync(new PriceBatchInput(products, category, mode, value), _staff);

    [Fact]
    public async Task A_percent_rise_rounds_to_a_thousand_and_logs_each_change()
    {
        var result = await BatchAsync(PriceBatchMode.Percent, 3m, products: _phone);

        // 24.990.000 + 3% = 25.739.700, 25.490.000 + 3% = 26.254.700, 19.990.000 + 3% = 20.589.700
        Assert.Equal((PriceBatchOutcome.Done, 3), (result.Outcome, result.Changed));
        var prices = Prices();
        Assert.Equal((25_740_000m, 26_255_000m, 20_590_000m, (decimal?)null), (prices["BLUE"], prices["BLACK"], prices["OLD"], prices["ASK"]));
        var log = Log();
        Assert.Equal(3, log.Count);
        var blue = log.Single(c => c.VariantId == _blue);
        Assert.Equal((24_990_000m, 25_740_000m, PriceChangeSource.Batch, _staff, Now.UtcDateTime),
            (blue.OldPrice, blue.NewPrice, blue.Source, blue.ChangedByUserId, blue.ChangedAt));
    }

    [Fact]
    public async Task An_amount_is_taken_off_as_is()
    {
        await BatchAsync(PriceBatchMode.Amount, -500_500m, products: _phone);

        Assert.Equal(24_489_500m, Prices()["BLUE"]);
    }

    [Fact]
    public async Task A_category_covers_every_product_in_it_and_nothing_else()
    {
        var result = await BatchAsync(PriceBatchMode.Amount, 10_000m, category: _iphoneCategory);

        Assert.Equal(4, result.Changed);
        var prices = Prices();
        Assert.Equal((1_000_000m, 3_490_000m), (prices["CASE"], prices["PODS"]));
    }

    [Fact]
    public async Task A_price_that_would_reach_zero_stops_the_whole_batch()
    {
        var before = Prices();

        var result = await BatchAsync(PriceBatchMode.Amount, -1_000_000m, category: _iphoneCategory);

        Assert.Equal((PriceBatchOutcome.PriceTooLow, "CASE"), (result.Outcome, result.Sku));
        Assert.Equal(before, Prices());
        Assert.Empty(Log());
    }

    [Theory]
    [InlineData(PriceBatchMode.Percent, 0)]
    [InlineData(PriceBatchMode.Amount, 0)]
    [InlineData(PriceBatchMode.Percent, -100)]
    public async Task A_change_of_nothing_or_all_of_it_is_refused(PriceBatchMode mode, int value)
    {
        Assert.Equal(PriceBatchOutcome.InvalidValue, (await BatchAsync(mode, value, products: _phone)).Outcome);
        Assert.Empty(Log());
    }

    // Review 2026-10-09: a huge number must be refused, not crash the page or
    // store a price the decimal(12,2) column cannot hold.
    [Theory]
    [InlineData(PriceBatchMode.Percent, "1000000000000000000000000000")]
    [InlineData(PriceBatchMode.Amount, "10000000000000")]
    public async Task A_price_beyond_what_the_shop_stores_is_refused(PriceBatchMode mode, string value)
    {
        var before = Prices();

        var result = await BatchAsync(mode, decimal.Parse(value), products: _phone);

        Assert.Equal(PriceBatchOutcome.PriceTooHigh, result.Outcome);
        Assert.Equal(before, Prices());
        Assert.Empty(Log());
    }

    [Fact]
    public async Task A_mode_that_does_not_exist_is_refused()
    {
        Assert.Equal(PriceBatchOutcome.InvalidValue, (await BatchAsync((PriceBatchMode)5, 1.5m, products: _phone)).Outcome);
        Assert.Empty(Log());
    }

    // A sale takes stock while the batch runs: stock moves, the version does
    // not, so the batch still goes through and the sale's stock stays taken.
    [Fact]
    public async Task A_sale_during_the_batch_neither_stops_it_nor_loses_its_stock()
    {
        _race.Arm("UPDATE \"ProductVariants\"", $"UPDATE ProductVariants SET StockQty = StockQty - 1 WHERE Id = {_black}");

        var result = await BatchAsync(PriceBatchMode.Percent, 3m, products: _phone);

        Assert.True(_race.Ran);
        Assert.Equal(PriceBatchOutcome.Done, result.Outcome);
        using var db = _shop.Context();
        var black = db.ProductVariants.Single(v => v.Id == _black);
        Assert.Equal((26_255_000m, 2), (black.Price, black.StockQty));
    }

    [Fact]
    public async Task Nothing_picked_is_refused()
    {
        Assert.Equal(PriceBatchOutcome.NothingSelected, (await BatchAsync(PriceBatchMode.Percent, 5m)).Outcome);
    }

    // 0.01% of 990.000 is 99 dong; rounded to a thousand the price stays.
    [Fact]
    public async Task A_price_rounding_back_to_itself_is_neither_saved_nor_logged()
    {
        var result = await BatchAsync(PriceBatchMode.Percent, 0.01m, products: _db.ProductVariants.Where(v => v.Id == _case).Select(v => v.ProductId).Single());

        Assert.Equal(PriceBatchOutcome.NothingToChange, result.Outcome);
        Assert.Empty(Log());
    }

    // An admin saves the black variant while the batch runs: the batch must not
    // overwrite it, and must not leave the blue one half done.
    [Fact]
    public async Task An_edit_landing_during_the_batch_stops_it_without_a_half_change()
    {
        _race.Arm("UPDATE \"ProductVariants\"", $"UPDATE ProductVariants SET Price = 26000000, UpdatedAt = '2030-01-01 00:00:00' WHERE Id = {_black}");

        var result = await BatchAsync(PriceBatchMode.Percent, 3m, products: _phone);

        Assert.True(_race.Ran);
        Assert.Equal(PriceBatchOutcome.Changed, result.Outcome);
        Assert.Equal(24_990_000m, Prices()["BLUE"]);
        Assert.Empty(Log());
    }

    // A batch moves each variant's version, so an admin page opened before it
    // cannot save the old price back over the new one.
    [Fact]
    public async Task An_edit_page_opened_before_the_batch_is_refused_after_it()
    {
        var catalog = new AdminCatalogService(_db, new EmptyLibrary(), new FixedTime(Now));
        var seen = (await catalog.GetAsync(_phone))!.Variants.Single(v => v.Id == _blue);

        await BatchAsync(PriceBatchMode.Percent, 3m, products: _phone);
        var stale = await catalog.UpdateVariantAsync(_blue, new VariantChange(24_990_000m, seen.StockQty, true, seen.StockQty, seen.Version), _staff);

        Assert.Equal(AdminCatalogOutcome.Changed, stale.Outcome);
        Assert.Equal(25_740_000m, Prices()["BLUE"]);
    }

    [Fact]
    public async Task History_lists_newest_first_with_who_and_can_show_one_product()
    {
        await BatchAsync(PriceBatchMode.Amount, 1_000m, products: _pods);
        await BatchAsync(PriceBatchMode.Amount, 2_000m, products: _phone);

        var all = await _sut.HistoryAsync();
        var pods = await _sut.HistoryAsync(_pods);

        Assert.Equal(4, all.Count);
        Assert.Equal("iPhone 17", all[0].ProductName);
        Assert.Equal("AirPods 4", all[^1].ProductName);
        var row = Assert.Single(pods);
        Assert.Equal(("PODS", 3_490_000m, 3_491_000m, "Staff Member", PriceChangeSource.Batch), (row.Sku, row.OldPrice, row.NewPrice, row.ChangedBy, row.Source));
    }

    private sealed class EmptyLibrary : IImageLibrary
    {
        public IReadOnlyList<string> All() => [];
    }
}
