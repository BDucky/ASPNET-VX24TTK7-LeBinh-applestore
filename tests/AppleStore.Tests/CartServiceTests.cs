using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// CartService over a real SQLite schema, so the unique indexes on Carts.UserId
// and CartItems (CartId, VariantId) take part. A race is staged by letting a
// second context write just before the service's own save.
public sealed class CartServiceTests : IDisposable
{
    private readonly ShopTestDb _shop = new();
    private readonly RaceInterceptor _race = new();
    private readonly AppDbContext _db;
    private readonly CartService _sut;
    private readonly User _alice;
    private readonly User _bob;
    private readonly Product _iphone;
    private readonly ProductVariant _blue;
    private readonly ProductVariant _black;

    public CartServiceTests()
    {
        _db = _shop.Context(_race);

        _alice = ShopTestDb.NewUser("alice@example.com");
        _bob = ShopTestDb.NewUser("bob@example.com");
        var category = NewCategory("iPhone", "iphone");
        _iphone = NewProduct(category, "iPhone 17", "iphone-17", 20_000_000m);
        _blue = AddVariant(_db, _iphone, "IP17-256-BLUE", 24_990_000m, stock: 5, config: "256GB", color: "Blue", region: "VN/A");
        _black = AddVariant(_db, _iphone, "IP17-256-BLACK", 24_990_000m, stock: 5, config: "256GB", color: "Black", region: "VN/A");
        _db.AddRange(_alice, _bob);
        _db.Add(new ProductImage { Product = _iphone, ImageUrl = "/img/iphone-17.png", SortOrder = 0 });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        _sut = new CartService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private AppDbContext NewContext() => _shop.Context();

    private ProductVariant Variant(string sku, decimal? price, int stock, bool status = true, string? config = "256GB")
    {
        var variant = AddVariant(_db, _db.Products.Single(p => p.Id == _iphone.Id), sku, price, stock, config: config, status: status);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return variant;
    }

    // What is stored, read through a fresh context so nothing is served from the tracker.
    private List<(int UserId, int VariantId, int Quantity)> Stored()
    {
        using var db = NewContext();
        return db.CartItems.OrderBy(i => i.Id).Select(i => new { i.Cart.UserId, i.VariantId, i.Quantity }).AsEnumerable()
            .Select(i => (i.UserId, i.VariantId, i.Quantity)).ToList();
    }

    private int CartCount()
    {
        using var db = NewContext();
        return db.Carts.Count();
    }

    // ---------- Add ----------

    [Fact]
    public async Task Adding_creates_the_cart_and_one_line()
    {
        var result = await _sut.AddAsync(_alice.Id, _blue.Id, 1);

        Assert.Equal(CartResult.Ok, result);
        Assert.Equal([(_alice.Id, _blue.Id, 1)], Stored());
        Assert.Equal(1, await _sut.CountAsync(_alice.Id));
    }

    [Fact]
    public async Task Adding_the_same_variant_again_raises_its_quantity()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 1);
        await _sut.AddAsync(_alice.Id, _blue.Id, 2);

        Assert.Equal([(_alice.Id, _blue.Id, 3)], Stored());
        Assert.Equal(1, CartCount());
    }

    [Fact]
    public async Task Each_variant_has_its_own_line_and_each_user_their_own_cart()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 1);
        await _sut.AddAsync(_alice.Id, _black.Id, 1);
        await _sut.AddAsync(_bob.Id, _blue.Id, 2);

        Assert.Equal([(_alice.Id, _blue.Id, 1), (_alice.Id, _black.Id, 1), (_bob.Id, _blue.Id, 2)], Stored());
        Assert.Equal(2, await _sut.CountAsync(_alice.Id));
        Assert.Equal(2, await _sut.CountAsync(_bob.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_quantity_below_one_is_refused_and_creates_nothing(int quantity)
    {
        var result = await _sut.AddAsync(_alice.Id, _blue.Id, quantity);

        Assert.Equal(CartOutcome.InvalidQuantity, result.Outcome);
        Assert.Empty(Stored());
        Assert.Equal(0, CartCount());
    }

    [Fact]
    public async Task An_unknown_variant_is_not_found()
    {
        Assert.Equal(CartOutcome.NotFound, (await _sut.AddAsync(_alice.Id, 999_999, 1)).Outcome);
        Assert.Equal(0, CartCount());
    }

    [Fact]
    public async Task A_variant_taken_off_sale_is_unavailable()
    {
        var hidden = Variant("IP17-OLD", 1m, stock: 5, status: false);

        Assert.Equal(CartOutcome.Unavailable, (await _sut.AddAsync(_alice.Id, hidden.Id, 1)).Outcome);
        Assert.Empty(Stored());
    }

    [Fact]
    public async Task A_variant_of_a_product_taken_off_sale_is_unavailable()
    {
        await _db.Products.Where(p => p.Id == _iphone.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, false));

        Assert.Equal(CartOutcome.Unavailable, (await _sut.AddAsync(_alice.Id, _blue.Id, 1)).Outcome);
        Assert.Empty(Stored());
    }

    [Fact]
    public async Task A_variant_with_no_price_cannot_be_added()
    {
        var unpriced = Variant("IP17-NOPRICE", null, stock: 5);

        Assert.Equal(CartOutcome.NoPrice, (await _sut.AddAsync(_alice.Id, unpriced.Id, 1)).Outcome);
        Assert.Empty(Stored());
    }

    [Fact]
    public async Task A_variant_out_of_stock_cannot_be_added()
    {
        var soldOut = Variant("IP17-SOLDOUT", 1m, stock: 0);

        Assert.Equal(CartOutcome.OutOfStock, (await _sut.AddAsync(_alice.Id, soldOut.Id, 1)).Outcome);
        Assert.Empty(Stored());
    }

    [Fact]
    public async Task Adding_past_the_stock_is_refused_and_says_how_many_more_fit()
    {
        var scarce = Variant("IP17-SCARCE", 1m, stock: 3);
        await _sut.AddAsync(_alice.Id, scarce.Id, 2);

        var result = await _sut.AddAsync(_alice.Id, scarce.Id, 2);

        Assert.Equal(new CartResult(CartOutcome.ExceedsStock, Available: 1), result);
        Assert.Equal([(_alice.Id, scarce.Id, 2)], Stored());
    }

    // ---------- Add, two requests at once (two tabs, a double click) ----------

    [Fact]
    public async Task Another_request_adding_the_same_line_first_ends_in_one_line_with_both_quantities()
    {
        await _sut.AddAsync(_alice.Id, _black.Id, 1);
        _race.BeforeFirstSave = () => AddDirectly(_alice.Id, _blue.Id, 1);

        var result = await _sut.AddAsync(_alice.Id, _blue.Id, 2);

        Assert.True(_race.Ran);
        Assert.Equal(CartResult.Ok, result);
        Assert.Equal([(_alice.Id, _black.Id, 1), (_alice.Id, _blue.Id, 3)], Stored());
    }

    [Fact]
    public async Task Another_request_creating_the_cart_first_ends_in_one_cart()
    {
        _race.BeforeFirstSave = () => AddDirectly(_alice.Id, _blue.Id, 1);

        var result = await _sut.AddAsync(_alice.Id, _blue.Id, 1);

        Assert.True(_race.Ran);
        Assert.Equal(CartResult.Ok, result);
        Assert.Equal(1, CartCount());
        Assert.Equal([(_alice.Id, _blue.Id, 2)], Stored());
    }

    [Fact]
    public async Task Another_request_taking_the_last_unit_first_leaves_this_one_refused()
    {
        var lastOne = Variant("IP17-LAST", 1m, stock: 1);
        _race.BeforeFirstSave = () => AddDirectly(_alice.Id, lastOne.Id, 1);

        var result = await _sut.AddAsync(_alice.Id, lastOne.Id, 1);

        Assert.True(_race.Ran);
        Assert.Equal(new CartResult(CartOutcome.ExceedsStock, Available: 0), result);
        Assert.Equal([(_alice.Id, lastOne.Id, 1)], Stored());
    }

    // The competing request, written through its own context as another web request would.
    private void AddDirectly(int userId, int variantId, int quantity)
    {
        using var db = NewContext();
        var cart = db.Carts.SingleOrDefault(c => c.UserId == userId) ?? db.Carts.Add(new Cart { UserId = userId }).Entity;
        var productId = db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.ProductId).Single();
        db.CartItems.Add(new CartItem { Cart = cart, ProductId = productId, VariantId = variantId, Quantity = quantity });
        db.SaveChanges();
    }

    // ---------- Set quantity ----------

    private async Task<int> LineId(int userId, int variantId)
    {
        await using var db = NewContext();
        return await db.CartItems.Where(i => i.Cart.UserId == userId && i.VariantId == variantId).Select(i => i.Id).SingleAsync();
    }

    [Fact]
    public async Task Setting_a_quantity_replaces_it()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 1);

        var result = await _sut.SetQuantityAsync(_alice.Id, await LineId(_alice.Id, _blue.Id), 4);

        Assert.Equal(CartResult.Ok, result);
        Assert.Equal([(_alice.Id, _blue.Id, 4)], Stored());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Setting_a_quantity_below_one_is_refused(int quantity)
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 2);

        var result = await _sut.SetQuantityAsync(_alice.Id, await LineId(_alice.Id, _blue.Id), quantity);

        Assert.Equal(CartOutcome.InvalidQuantity, result.Outcome);
        Assert.Equal([(_alice.Id, _blue.Id, 2)], Stored());
    }

    [Fact]
    public async Task Setting_a_quantity_past_the_stock_is_refused_and_says_the_stock()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 2);

        var result = await _sut.SetQuantityAsync(_alice.Id, await LineId(_alice.Id, _blue.Id), 6);

        Assert.Equal(new CartResult(CartOutcome.ExceedsStock, Available: 5), result);
        Assert.Equal([(_alice.Id, _blue.Id, 2)], Stored());
    }

    [Fact]
    public async Task Another_users_line_cannot_be_changed()
    {
        await _sut.AddAsync(_bob.Id, _blue.Id, 1);

        var result = await _sut.SetQuantityAsync(_alice.Id, await LineId(_bob.Id, _blue.Id), 3);

        Assert.Equal(CartOutcome.NotFound, result.Outcome);
        Assert.Equal([(_bob.Id, _blue.Id, 1)], Stored());
    }

    [Fact]
    public async Task A_line_whose_variant_left_sale_can_only_be_removed()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 1);
        await _db.ProductVariants.Where(v => v.Id == _blue.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, false));

        var result = await _sut.SetQuantityAsync(_alice.Id, await LineId(_alice.Id, _blue.Id), 2);

        Assert.Equal(CartOutcome.Unavailable, result.Outcome);
        Assert.Equal([(_alice.Id, _blue.Id, 1)], Stored());
    }

    // ---------- Remove ----------

    [Fact]
    public async Task Removing_deletes_only_that_line()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 1);
        await _sut.AddAsync(_alice.Id, _black.Id, 1);

        var result = await _sut.RemoveAsync(_alice.Id, await LineId(_alice.Id, _blue.Id));

        Assert.Equal(CartResult.Ok, result);
        Assert.Equal([(_alice.Id, _black.Id, 1)], Stored());
    }

    [Fact]
    public async Task Removing_twice_or_another_users_line_is_not_found()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 1);
        await _sut.AddAsync(_bob.Id, _blue.Id, 1);
        var aliceLine = await LineId(_alice.Id, _blue.Id);
        var bobLine = await LineId(_bob.Id, _blue.Id);
        await _sut.RemoveAsync(_alice.Id, aliceLine);

        Assert.Equal(CartOutcome.NotFound, (await _sut.RemoveAsync(_alice.Id, aliceLine)).Outcome);
        Assert.Equal(CartOutcome.NotFound, (await _sut.RemoveAsync(_alice.Id, bobLine)).Outcome);
        Assert.Equal([(_bob.Id, _blue.Id, 1)], Stored());
    }

    // ---------- View ----------

    [Fact]
    public async Task An_empty_cart_reads_as_empty_and_is_not_created_by_reading()
    {
        var cart = await _sut.GetAsync(_alice.Id);

        Assert.Empty(cart.Lines);
        Assert.Equal(0m, cart.Subtotal);
        Assert.Equal(0, await _sut.CountAsync(_alice.Id));
        Assert.Equal(0, CartCount());
    }

    [Fact]
    public async Task The_cart_names_each_line_like_the_catalog_and_totals_it()
    {
        await _sut.AddAsync(_alice.Id, _blue.Id, 2);
        await _sut.AddAsync(_alice.Id, _black.Id, 1);

        var cart = await _sut.GetAsync(_alice.Id);

        var blue = cart.Lines[0];
        Assert.Equal(
            (_blue.Id, "iPhone 17", "iphone-17", "256GB", "256gb", "Blue", "VN/A", "/img/iphone-17.png", 24_990_000m, 2, 49_980_000m, CartLineProblem.None),
            (blue.VariantId, blue.ProductName, blue.ProductSlug, blue.ConfigurationName, blue.ConfigurationSlug, blue.Color, blue.Region, blue.ImageUrl, blue.UnitPrice, blue.Quantity, blue.LineTotal, blue.Problem));
        Assert.Equal(_black.Id, cart.Lines[1].VariantId);
        Assert.Equal(74_970_000m, cart.Subtotal);
        Assert.Equal(3, cart.ItemCount);
    }

    [Fact]
    public async Task A_variant_with_no_config_option_is_named_after_its_product()
    {
        var single = Variant("IP17-SINGLE", 1_000m, stock: 2, config: null);
        await _sut.AddAsync(_alice.Id, single.Id, 1);

        var line = Assert.Single((await _sut.GetAsync(_alice.Id)).Lines);

        Assert.Equal(("iPhone 17", "iphone-17"), (line.ConfigurationName, line.ConfigurationSlug));
    }

    // A line already in the cart can stop being buyable: it stays visible
    // with the reason, and stops counting towards the subtotal.
    [Fact]
    public async Task Lines_that_can_no_longer_be_bought_say_why_and_leave_the_subtotal()
    {
        var hidden = Variant("IP17-H", 1_000m, stock: 5);
        var unpriced = Variant("IP17-P", 1_000m, stock: 5);
        var soldOut = Variant("IP17-S", 1_000m, stock: 5);
        var short_ = Variant("IP17-N", 1_000m, stock: 5);
        foreach (var v in new[] { _blue, hidden, unpriced, soldOut, short_ })
            await _sut.AddAsync(_alice.Id, v.Id, 2);
        await _db.ProductVariants.Where(v => v.Id == hidden.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, false));
        await _db.ProductVariants.Where(v => v.Id == unpriced.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, (decimal?)null));
        await _db.ProductVariants.Where(v => v.Id == soldOut.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, 0));
        await _db.ProductVariants.Where(v => v.Id == short_.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, 1));

        var cart = await _sut.GetAsync(_alice.Id);

        Assert.Equal(
            [CartLineProblem.None, CartLineProblem.Unavailable, CartLineProblem.NoPrice, CartLineProblem.OutOfStock, CartLineProblem.NotEnoughStock],
            cart.Lines.Select(l => l.Problem));
        Assert.Equal(49_980_000m, cart.Subtotal);
        Assert.Equal(10, cart.ItemCount);
    }

    // Runs an action once, just before the first SaveChanges of the context it is attached to.
    private sealed class RaceInterceptor : SaveChangesInterceptor
    {
        public Action? BeforeFirstSave { get; set; }
        public bool Ran { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (BeforeFirstSave is { } action && !Ran)
            {
                Ran = true;
                action();
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
