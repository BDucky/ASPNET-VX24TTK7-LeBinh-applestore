using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// CompareService on SQLite with the real ReviewService, so the rating in a
// column follows the same "visible reviews only" rule as the product page.
public sealed class CompareServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly CompareService _sut;
    private readonly int _phone;
    private readonly int _pods;
    private readonly int _watch;
    private readonly int _hidden;
    private readonly int _bare;

    public CompareServiceTests()
    {
        _db = _shop.Context();
        var iphone = NewCategory("iPhone", "iphone");
        var phone = NewProduct(iphone, "iPhone 17", "iphone-17", 1m);
        AddVariant(_db, phone, "P1", 24_990_000m, 0, config: "iPhone 17 256GB", color: "Blue", region: "VN/A");
        AddVariant(_db, phone, "P2", 25_490_000m, 4, config: "iPhone 17 256GB", color: "Black", region: "LL/A");
        AddVariant(_db, phone, "P3", 29_990_000m, 2, config: "iPhone 17 512GB", color: "Blue", region: "VN/A");
        // Inactive: its lower price, colour and stock must not show.
        AddVariant(_db, phone, "P4", 1_000m, 9, config: "iPhone 17 128GB", color: "Gold", region: "ZA/A", status: false);
        var pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods Pro 3", "airpods-pro-3", 1m);
        AddVariant(_db, pods, "A1", 6_490_000m, 0);
        var watch = NewProduct(NewCategory("Watch", "watch"), "Apple Watch 12", "watch-12", 1m);
        AddVariant(_db, watch, "W1", 10_990_000m, 1);
        var hidden = NewProduct(iphone, "iPhone 15", "iphone-15", 1m, status: false);
        AddVariant(_db, hidden, "H1", 15_000_000m, 3);
        var bare = NewProduct(iphone, "iPhone 18", "iphone-18", 1m);
        _db.ProductImages.Add(new ProductImage { Product = phone, ImageUrl = "/img/second.jpg", SortOrder = 2 });
        _db.ProductImages.Add(new ProductImage { Product = phone, ImageUrl = "/img/first.jpg", SortOrder = 1 });
        var alice = ShopTestDb.NewUser("alice@example.com");
        var bob = ShopTestDb.NewUser("bob@example.com");
        var carol = ShopTestDb.NewUser("carol@example.com");
        _db.AddRange(bare, alice, bob, carol);
        _db.SaveChanges();
        AddReview(phone.Id, alice.Id, 5, visible: true);
        AddReview(phone.Id, bob.Id, 4, visible: true);
        AddReview(phone.Id, carol.Id, 1, visible: false);
        (_phone, _pods, _watch, _hidden, _bare) = (phone.Id, pods.Id, watch.Id, hidden.Id, bare.Id);
        _sut = new CompareService(_db, new ReviewService(_db, new FixedTime(Now)));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private void AddReview(int productId, int userId, int rating, bool visible)
    {
        _db.Reviews.Add(new Review { ProductId = productId, UserId = userId, Rating = rating, Status = visible, CreatedAt = Now.UtcDateTime });
        _db.SaveChanges();
    }

    [Fact]
    public async Task BuildAsync_fills_a_column_from_active_variants_and_visible_reviews()
    {
        var column = Assert.Single(await _sut.BuildAsync([_phone]));

        Assert.Equal(("iPhone 17", "iphone-17", "iPhone", "/img/first.jpg"), (column.Name, column.Slug, column.CategoryName, column.ImageUrl));
        Assert.Equal(24_990_000m, column.FromPrice);
        Assert.True(column.InStock);
        Assert.Equal(["iPhone 17 256GB", "iPhone 17 512GB"], column.Configurations);
        Assert.Equal(["Blue", "Black"], column.Colors);
        Assert.Equal(["VN/A", "LL/A"], column.Regions);
        Assert.Equal((4.5, 2), (column.AverageRating, column.ReviewCount));
    }

    [Fact]
    public async Task BuildAsync_keeps_the_order_given_and_mixes_categories()
    {
        var columns = await _sut.BuildAsync([_watch, _phone, _pods]);

        Assert.Equal([_watch, _phone, _pods], columns.Select(c => c.ProductId));
    }

    [Fact]
    public async Task BuildAsync_leaves_out_hidden_and_unknown_products()
    {
        var columns = await _sut.BuildAsync([_hidden, 999_999, _pods]);

        Assert.Equal([_pods], columns.Select(c => c.ProductId));
    }

    [Fact]
    public async Task BuildAsync_shows_a_product_with_no_variants_or_reviews_as_empty_not_missing()
    {
        var bare = Assert.Single(await _sut.BuildAsync([_bare]));
        var pods = Assert.Single(await _sut.BuildAsync([_pods]));

        Assert.Equal((null, false, 0, null, 0), (bare.FromPrice, bare.InStock, bare.Configurations.Count, bare.AverageRating, bare.ReviewCount));
        Assert.False(pods.InStock);
        Assert.Empty(pods.Colors);
    }

    [Fact]
    public async Task AddAsync_appends_a_product_from_another_category()
    {
        var result = await _sut.AddAsync([_phone], _pods);

        Assert.Equal(CompareOutcome.Added, result.Outcome);
        Assert.Equal([_phone, _pods], result.Ids);
    }

    [Fact]
    public async Task AddAsync_twice_keeps_one_copy()
    {
        var result = await _sut.AddAsync([_phone, _pods], _phone);

        Assert.Equal(CompareOutcome.AlreadyIn, result.Outcome);
        Assert.Equal([_phone, _pods], result.Ids);
    }

    [Fact]
    public async Task AddAsync_refuses_a_fourth_product_and_keeps_the_three()
    {
        var result = await _sut.AddAsync([_phone, _pods, _watch], _bare);

        Assert.Equal(CompareOutcome.Full, result.Outcome);
        Assert.Equal([_phone, _pods, _watch], result.Ids);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddAsync_refuses_a_hidden_or_unknown_product(bool hidden)
    {
        var result = await _sut.AddAsync([_phone], hidden ? _hidden : 999_999);

        Assert.Equal(CompareOutcome.NotFound, result.Outcome);
        Assert.Equal([_phone], result.Ids);
    }

    // A product hidden after it was added must not hold one of the three slots.
    [Fact]
    public async Task AddAsync_frees_the_slot_of_a_product_hidden_since()
    {
        var result = await _sut.AddAsync([_hidden, _phone, _pods], _watch);

        Assert.Equal(CompareOutcome.Added, result.Outcome);
        Assert.Equal([_phone, _pods, _watch], result.Ids);
    }
}
