using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// AdminCatalogService on SQLite, read back through the real
// ProductCatalogService so an admin's change is checked where shoppers see it.
public sealed class AdminCatalogServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly AdminCatalogService _sut;
    private readonly int _iphoneCategory;
    private readonly int _macCategory;
    private readonly int _phone;
    private readonly int _blue;

    public AdminCatalogServiceTests()
    {
        _db = _shop.Context();
        var iphone = NewCategory("iPhone", "iphone");
        var mac = NewCategory("Mac", "mac");
        var product = NewProduct(iphone, "iPhone 17", "iphone-17", 20_000_000m);
        var blue = AddVariant(_db, product, "IP17-BLUE", 24_990_000m, stock: 5, config: "iPhone 17 256GB", color: "Blue", region: "VN/A");
        _db.Add(mac);
        _db.Add(new ProductImage { Product = product, ImageUrl = "/img/products/iphone-17.jpg", SortOrder = 0 });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        (_iphoneCategory, _macCategory, _phone, _blue) = (iphone.Id, mac.Id, product.Id, blue.Id);
        _sut = new AdminCatalogService(_db, new FakeLibrary(), new FixedTime(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private sealed class FakeLibrary : IImageLibrary
    {
        public IReadOnlyList<string> All() => ["/img/products/iphone-17.jpg", "/img/products/mac-mini.jpg"];
    }

    private ProductCatalogService Shop() => new(_shop.Context());

    private ProductInput Input(string? name = "Mac mini", string? image = "/img/products/mac-mini.jpg", int? category = null, decimal? basePrice = 14_990_000m, bool onSale = true) =>
        new(name, "Small desktop", category ?? _macCategory, basePrice, onSale, image);

    private async Task<long> VersionAsync(int productId) => (await _sut.GetAsync(productId))!.Version;

    private void Run(string sql)
    {
        using var db = _shop.Context();
        db.Database.ExecuteSqlRaw(sql);
    }

    // ---------- Products ----------

    [Fact]
    public async Task A_new_product_gets_a_slug_and_its_photo_and_shows_in_the_shop()
    {
        var result = await _sut.CreateAsync(Input(name: "  Mac mini M5 ( Mỹ ) "));

        Assert.Equal(AdminCatalogOutcome.Done, result.Outcome);
        var edit = (await _sut.GetAsync(result.Id!.Value))!;
        Assert.Equal(("Mac mini M5 ( Mỹ )", "mac-mini-m5-my", _macCategory, "/img/products/mac-mini.jpg", true),
            (edit.Name, edit.Slug, edit.CategoryId, edit.ImageUrl, edit.OnSale));
        var detail = await Shop().GetBySlugAsync("mac-mini-m5-my");
        Assert.Equal(["/img/products/mac-mini.jpg"], detail!.ImageUrls);
    }

    [Theory]
    [InlineData(null, AdminCatalogOutcome.MissingName)]
    [InlineData("   ", AdminCatalogOutcome.MissingName)]
    [InlineData("iPhone 17", AdminCatalogOutcome.NameTaken)]
    [InlineData("IPHONE-17", AdminCatalogOutcome.NameTaken)]
    public async Task A_name_must_be_given_and_not_clash_with_another_products_page(string? name, AdminCatalogOutcome expected)
    {
        Assert.Equal(expected, (await _sut.CreateAsync(Input(name: name))).Outcome);
        Assert.Equal(1, _db.Products.Count());
    }

    [Fact]
    public async Task Category_photo_and_price_are_checked()
    {
        Assert.Equal(AdminCatalogOutcome.UnknownCategory, (await _sut.CreateAsync(Input(category: 999))).Outcome);
        Assert.Equal(AdminCatalogOutcome.ImageNotInLibrary, (await _sut.CreateAsync(Input(image: "https://evil.example/x.jpg"))).Outcome);
        Assert.Equal(AdminCatalogOutcome.ImageNotInLibrary, (await _sut.CreateAsync(Input(image: "/img/products/../../appsettings.json"))).Outcome);
        Assert.Equal(AdminCatalogOutcome.InvalidPrice, (await _sut.CreateAsync(Input(basePrice: 0m))).Outcome);
        Assert.Equal(AdminCatalogOutcome.InvalidPrice, (await _sut.CreateAsync(Input(basePrice: -1m))).Outcome);
        Assert.Equal(1, _db.Products.Count());
    }

    [Fact]
    public async Task A_product_without_a_photo_or_price_is_allowed()
    {
        var result = await _sut.CreateAsync(Input(image: null, basePrice: null));

        Assert.Equal(AdminCatalogOutcome.Done, result.Outcome);
        Assert.Null((await _sut.GetAsync(result.Id!.Value))!.ImageUrl);
    }

    [Fact]
    public async Task Editing_changes_the_product_and_its_photo_but_keeps_its_page_address()
    {
        var result = await _sut.UpdateAsync(_phone, Input(name: "iPhone 17 (2026)", category: _iphoneCategory), await VersionAsync(_phone));

        Assert.Equal(AdminCatalogOutcome.Done, result.Outcome);
        var edit = (await _sut.GetAsync(_phone))!;
        Assert.Equal(("iPhone 17 (2026)", "iphone-17", "/img/products/mac-mini.jpg"), (edit.Name, edit.Slug, edit.ImageUrl));
        await using var db = _shop.Context();
        Assert.Equal(1, await db.ProductImages.CountAsync(i => i.ProductId == _phone));
    }

    [Fact]
    public async Task Removing_the_photo_leaves_the_product_without_one()
    {
        await _sut.UpdateAsync(_phone, Input(name: "iPhone 17", category: _iphoneCategory, image: null), await VersionAsync(_phone));

        Assert.Null((await _sut.GetAsync(_phone))!.ImageUrl);
    }

    // Two admins open the same product; the second save must not silently
    // overwrite the first.
    [Fact]
    public async Task An_edit_made_meanwhile_is_not_overwritten()
    {
        var seen = await VersionAsync(_phone);
        await _sut.UpdateAsync(_phone, Input(name: "First admin", category: _iphoneCategory), seen);

        var second = await _sut.UpdateAsync(_phone, Input(name: "Second admin", category: _iphoneCategory), seen);

        Assert.Equal(AdminCatalogOutcome.Changed, second.Outcome);
        Assert.Equal("First admin", (await _sut.GetAsync(_phone))!.Name);
    }

    // The shop clock reads the same instant for both saves (FixedTime), as two
    // saves in one clock tick would: the version must still move.
    [Fact]
    public async Task Two_saves_in_the_same_instant_still_catch_the_stale_one()
    {
        Run($"UPDATE Products SET UpdatedAt = '{Now.UtcDateTime:yyyy-MM-dd HH:mm:ss}' WHERE Id = {_phone}");
        Run($"UPDATE ProductVariants SET UpdatedAt = '{Now.UtcDateTime:yyyy-MM-dd HH:mm:ss}' WHERE Id = {_blue}");
        var seen = (await _sut.GetAsync(_phone))!;
        var variant = seen.Variants.Single();

        await _sut.UpdateAsync(_phone, Input(name: "First", category: _iphoneCategory), seen.Version);
        await _sut.UpdateVariantAsync(_blue, new VariantChange(1m, 5, true, variant.StockQty, variant.Version));

        Assert.Equal(AdminCatalogOutcome.Changed, (await _sut.UpdateAsync(_phone, Input(name: "Second", category: _iphoneCategory), seen.Version)).Outcome);
        Assert.Equal(AdminCatalogOutcome.Changed, (await _sut.UpdateVariantAsync(_blue, new VariantChange(2m, 5, true, variant.StockQty, variant.Version))).Outcome);
    }

    // Taken off sale in the same instant the edit page was opened: saving
    // that page must not quietly put it back on sale.
    [Fact]
    public async Task Taking_off_sale_moves_the_version_so_an_open_edit_page_is_stale()
    {
        Run($"UPDATE Products SET UpdatedAt = '{Now.UtcDateTime:yyyy-MM-dd HH:mm:ss}' WHERE Id = {_phone}");
        var seen = await VersionAsync(_phone);

        await _sut.SetOnSaleAsync(_phone, false);

        Assert.Equal(AdminCatalogOutcome.Changed, (await _sut.UpdateAsync(_phone, Input(name: "iPhone 17", category: _iphoneCategory), seen)).Outcome);
        Assert.False((await _sut.GetAsync(_phone))!.OnSale);
    }

    [Fact]
    public async Task Taking_a_product_off_sale_hides_it_from_the_shop_and_back()
    {
        Assert.Equal(AdminCatalogOutcome.Done, (await _sut.SetOnSaleAsync(_phone, false)).Outcome);
        Assert.Null(await Shop().GetBySlugAsync("iphone-17"));
        Assert.Equal(1, _db.Products.Count());

        await _sut.SetOnSaleAsync(_phone, true);
        Assert.NotNull(await Shop().GetBySlugAsync("iphone-17"));
        Assert.Equal(AdminCatalogOutcome.NotFound, (await _sut.SetOnSaleAsync(999, false)).Outcome);
    }

    [Fact]
    public async Task The_list_finds_products_by_name_without_regard_to_case()
    {
        await _sut.CreateAsync(Input());

        var all = await _sut.ListAsync(null);
        var found = await _sut.ListAsync("MAC");

        Assert.Equal(["iPhone 17", "Mac mini"], all.Select(p => p.Name).Order());
        var mac = Assert.Single(found);
        Assert.Equal(("Mac", 0, 0), (mac.Category, mac.Variants, mac.Stock));
        Assert.Equal((1, 5), (all.Single(p => p.Name == "iPhone 17").Variants, all.Single(p => p.Name == "iPhone 17").Stock));
    }

    // ---------- Variants ----------

    [Fact]
    public async Task A_new_variant_shows_on_its_configuration_page()
    {
        var result = await _sut.AddVariantAsync(_phone, new VariantInput(" IP17-PINK ", "iPhone 17 256GB", "Pink", "VN/A", 24_990_000m, 3, true));

        Assert.Equal(AdminCatalogOutcome.Done, result.Outcome);
        var configuration = await Shop().GetConfigurationAsync("iphone-17", "iphone-17-256gb");
        var pink = Assert.Single(configuration!.Choices, c => c.Color == "Pink");
        Assert.Equal(("IP17-PINK", 24_990_000m, 3), (pink.SKU, pink.Price, pink.StockQty));
    }

    [Theory]
    [InlineData(" ", 1, 1, AdminCatalogOutcome.MissingSku)]
    [InlineData("ip17-blue", 1, 1, AdminCatalogOutcome.SkuTaken)]
    [InlineData("NEW", 0, 1, AdminCatalogOutcome.InvalidPrice)]
    [InlineData("NEW", 1, -1, AdminCatalogOutcome.InvalidStock)]
    public async Task A_new_variant_needs_a_free_sku_a_positive_price_and_stock_of_zero_or_more(string sku, int price, int stock, AdminCatalogOutcome expected)
    {
        var result = await _sut.AddVariantAsync(_phone, new VariantInput(sku, "iPhone 17 256GB", "Pink", null, price, stock, true));

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, _db.ProductVariants.Count());
    }

    [Fact]
    public async Task A_variant_for_an_unknown_product_is_not_found()
    {
        Assert.Equal(AdminCatalogOutcome.NotFound, (await _sut.AddVariantAsync(999, new VariantInput("X", null, null, null, 1m, 1, true))).Outcome);
    }

    [Fact]
    public async Task Changing_a_variant_sets_price_stock_and_sale()
    {
        var seen = (await _sut.GetAsync(_phone))!.Variants.Single();

        var result = await _sut.UpdateVariantAsync(_blue, new VariantChange(23_990_000m, 12, false, seen.StockQty, seen.Version));

        Assert.Equal(AdminCatalogOutcome.Done, result.Outcome);
        var after = (await _sut.GetAsync(_phone))!.Variants.Single();
        Assert.Equal((23_990_000m, 12, false), (after.Price, after.StockQty, after.OnSale));
    }

    // The admin saw 5 in stock; a customer bought 2 before the save landed.
    [Fact]
    public async Task A_sale_made_while_the_admin_typed_is_not_overwritten()
    {
        var seen = (await _sut.GetAsync(_phone))!.Variants.Single();
        Run($"UPDATE ProductVariants SET StockQty = 3 WHERE Id = {_blue}");

        var result = await _sut.UpdateVariantAsync(_blue, new VariantChange(24_990_000m, 10, true, seen.StockQty, seen.Version));

        Assert.Equal(new AdminCatalogResult(AdminCatalogOutcome.Changed, CurrentStock: 3), result);
        Assert.Equal(3, (await _sut.GetAsync(_phone))!.Variants.Single().StockQty);
    }

    [Fact]
    public async Task Another_admins_variant_edit_is_not_overwritten()
    {
        var seen = (await _sut.GetAsync(_phone))!.Variants.Single();
        await _sut.UpdateVariantAsync(_blue, new VariantChange(1m, 5, true, seen.StockQty, seen.Version));

        var second = await _sut.UpdateVariantAsync(_blue, new VariantChange(2m, 5, true, seen.StockQty, seen.Version));

        Assert.Equal(AdminCatalogOutcome.Changed, second.Outcome);
        Assert.Equal(1m, (await _sut.GetAsync(_phone))!.Variants.Single().Price);
    }

    [Theory]
    [InlineData(0, 1, AdminCatalogOutcome.InvalidPrice)]
    [InlineData(1, -1, AdminCatalogOutcome.InvalidStock)]
    public async Task A_variant_change_is_checked_like_a_new_one(int price, int stock, AdminCatalogOutcome expected)
    {
        var seen = (await _sut.GetAsync(_phone))!.Variants.Single();

        Assert.Equal(expected, (await _sut.UpdateVariantAsync(_blue, new VariantChange(price, stock, true, seen.StockQty, seen.Version))).Outcome);
        Assert.Equal(24_990_000m, (await _sut.GetAsync(_phone))!.Variants.Single().Price);
    }

    [Fact]
    public async Task A_variant_can_be_left_without_a_price_and_shows_contact_for_price()
    {
        var seen = (await _sut.GetAsync(_phone))!.Variants.Single();

        Assert.Equal(AdminCatalogOutcome.Done, (await _sut.UpdateVariantAsync(_blue, new VariantChange(null, 5, true, seen.StockQty, seen.Version))).Outcome);
        Assert.Null((await Shop().GetConfigurationAsync("iphone-17", "iphone-17-256gb"))!.Choices.Single().Price);
    }
}
