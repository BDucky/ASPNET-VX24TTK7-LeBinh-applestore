using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;

namespace AppleStore.Tests;

public class ProductCatalogServiceTests
{
    private static (ProductCatalogService Sut, SqliteInMemoryFixture Fixture) CreateSut()
    {
        var fixture = new SqliteInMemoryFixture();
        fixture.Context.Database.EnsureCreated();
        var sut = new ProductCatalogService(fixture.Context);
        return (sut, fixture);
    }

    // Also used by AdminAreaTests to put products in the web app's database.
    internal static Category NewCategory(string name, string slug) =>
        new() { Name = name, Slug = slug };

    internal static Product NewProduct(Category category, string name, string slug, decimal basePrice, bool status = true, int sortOrder = 0) =>
        new()
        {
            SortOrder = sortOrder,
            Category = category,
            Name = name,
            Slug = slug,
            BasePrice = basePrice,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    internal static ProductVariant NewVariant(Product product, string sku, decimal? price, int stock, bool status = true) =>
        new()
        {
            Product = product,
            SKU = sku,
            Price = price,
            StockQty = stock,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    // Tags a variant with an option ("config", "color", "region"), reusing
    // the option type and value rows when they already exist.
    internal static void Tag(AppleStore.Infrastructure.Data.AppDbContext db, ProductVariant variant, string code, string value)
    {
        var type = db.ChangeTracker.Entries<OptionType>().Select(e => e.Entity).FirstOrDefault(t => t.Code == code)
            ?? new OptionType { Code = code };
        var optionValue = db.ChangeTracker.Entries<OptionValue>().Select(e => e.Entity).FirstOrDefault(v => v.OptionType == type && v.Value == value)
            ?? new OptionValue { OptionType = type, Value = value };
        db.Add(new VariantOption { Variant = variant, OptionType = type, OptionValue = optionValue });
    }

    private static ProductVariant AddVariant(SqliteInMemoryFixture f, Product product, string sku, decimal? price, int stock, string? config = null, string? color = null, string? region = null, bool status = true) =>
        AddVariant(f.Context, product, sku, price, stock, config, color, region, status);

    // Also used by the cart tests.
    internal static ProductVariant AddVariant(AppleStore.Infrastructure.Data.AppDbContext db, Product product, string sku, decimal? price, int stock, string? config = null, string? color = null, string? region = null, bool status = true)
    {
        var v = NewVariant(product, sku, price, stock, status);
        db.Add(v);
        if (config is not null) Tag(db, v, "config", config);
        if (color is not null) Tag(db, v, "color", color);
        if (region is not null) Tag(db, v, "region", region);
        return v;
    }

    [Fact]
    public async Task GetProductsAsync_returns_all_active_products_by_default()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var p1 = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        var p2 = NewProduct(iphone, "iPhone 17 Pro", "iphone-17-pro", 1199m);
        fixture.Context.AddRange(p1, p2);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetProductsAsync_excludes_inactive_products()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var mac = NewCategory("Mac", "mac");
        var active = NewProduct(mac, "MacBook Air", "macbook-air", 1099m);
        var inactive = NewProduct(mac, "Discontinued Mac", "discontinued-mac", 899m, status: false);
        fixture.Context.AddRange(active, inactive);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Single(result);
        Assert.Equal("macbook-air", result[0].Slug);
    }

    [Fact]
    public async Task GetProductsAsync_filters_by_category_slug()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var mac = NewCategory("Mac", "mac");
        fixture.Context.AddRange(
            NewProduct(iphone, "iPhone 17", "iphone-17", 999m),
            NewProduct(mac, "MacBook Air", "macbook-air", 1099m));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync(categorySlug: "mac");

        Assert.Single(result);
        Assert.Equal("macbook-air", result[0].Slug);
    }

    [Fact]
    public async Task GetProductsAsync_filters_by_search_query_case_insensitive()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        fixture.Context.AddRange(
            NewProduct(iphone, "iPhone 17 Pro", "iphone-17-pro", 1199m),
            NewProduct(iphone, "iPhone Air", "iphone-air", 999m));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync(query: "PRO");

        Assert.Single(result);
        Assert.Equal("iphone-17-pro", result[0].Slug);
    }

    [Fact]
    public async Task GetProductsAsync_from_price_is_lowest_active_variant_price()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var product = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        fixture.Context.Add(product);
        fixture.Context.AddRange(
            NewVariant(product, "IP17-128", 999m, 10),
            NewVariant(product, "IP17-256", 1099m, 5),
            NewVariant(product, "IP17-512-DISC", 899m, 0, status: false));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Equal(999m, result[0].FromPrice);
    }

    [Fact]
    public async Task GetProductsAsync_image_url_is_the_lowest_sort_order_image()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var product = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        fixture.Context.Add(product);
        fixture.Context.Add(new ProductImage { Product = product, ImageUrl = "/img/products/second.jpg", SortOrder = 1 });
        fixture.Context.Add(new ProductImage { Product = product, ImageUrl = "/img/products/first.jpg", SortOrder = 0 });
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Equal("/img/products/first.jpg", result[0].ImageUrl);
    }

    [Fact]
    public async Task GetProductsAsync_image_url_is_null_when_product_has_no_image()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        fixture.Context.Add(NewProduct(iphone, "iPhone 17", "iphone-17", 999m));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Null(result[0].ImageUrl);
    }

    [Fact]
    public async Task GetProductsAsync_from_price_ignores_variants_without_a_price()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var product = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 18 Pro Max", "iphone-18-pro-max", 0m);
        fixture.Context.Add(product);
        AddVariant(fixture, product, "A", null, 5);
        AddVariant(fixture, product, "B", 42_300_000m, 5);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Equal(42_300_000m, result[0].FromPrice);
    }

    // A model rauvang lists without any price shows "Contact for price", never
    // a made-up number such as the product's BasePrice or 0.
    [Fact]
    public async Task GetProductsAsync_from_price_is_null_when_no_variant_has_a_price()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var product = NewProduct(NewCategory("Watch", "watch"), "Apple Watch Ultra 4", "apple-watch-ultra-4", 999m);
        fixture.Context.Add(product);
        AddVariant(fixture, product, "A", null, 5);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Null(result[0].FromPrice);
    }

    [Fact]
    public async Task GetProductsAsync_featured_order_follows_sort_order()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        fixture.Context.AddRange(
            NewProduct(iphone, "iPhone 15", "iphone-15", 0m, sortOrder: 2),
            NewProduct(iphone, "iPhone Duo", "iphone-duo", 0m, sortOrder: 0),
            NewProduct(iphone, "iPhone 18 Pro Max", "iphone-18-pro-max", 0m, sortOrder: 1));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync();

        Assert.Equal(new[] { "iphone-duo", "iphone-18-pro-max", "iphone-15" }, result.Select(p => p.Slug));
    }

    [Fact]
    public async Task GetProductsAsync_sorts_by_price_ascending_with_contact_for_price_last()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var expensive = NewProduct(iphone, "Expensive", "expensive", 0m);
        var contact = NewProduct(iphone, "Contact", "contact", 0m);
        var cheap = NewProduct(iphone, "Cheap", "cheap", 0m);
        fixture.Context.AddRange(expensive, contact, cheap);
        AddVariant(fixture, expensive, "E", 60_000_000m, 1);
        AddVariant(fixture, contact, "C", null, 1);
        AddVariant(fixture, cheap, "H", 9_000_000m, 1);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync(sort: ProductSort.PriceAscending);

        Assert.Equal(new[] { "cheap", "expensive", "contact" }, result.Select(p => p.Slug));
    }

    [Fact]
    public async Task GetProductsAsync_sorts_by_price_descending_with_contact_for_price_last()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var cheap = NewProduct(iphone, "Cheap", "cheap", 0m);
        var contact = NewProduct(iphone, "Contact", "contact", 0m);
        var expensive = NewProduct(iphone, "Expensive", "expensive", 0m);
        fixture.Context.AddRange(cheap, contact, expensive);
        AddVariant(fixture, cheap, "H", 9_000_000m, 1);
        AddVariant(fixture, contact, "C", null, 1);
        AddVariant(fixture, expensive, "E", 60_000_000m, 1);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync(sort: ProductSort.PriceDescending);

        Assert.Equal(new[] { "expensive", "cheap", "contact" }, result.Select(p => p.Slug));
    }

    // Use case 8. Two products straddle bands: "split" has a 25M and a 45M
    // variant, "edge" sits exactly on 10M. "retired" has a cheap variant that
    // is off sale, "contact" has no price at all.
    private static async Task<(ProductCatalogService Sut, SqliteInMemoryFixture Fixture)> BandShopAsync()
    {
        var (sut, fixture) = CreateSut();
        var iphone = NewCategory("iPhone", "iphone");
        var mac = NewCategory("Mac", "mac");
        var cheap = NewProduct(iphone, "Cheap Phone", "cheap", 0m, sortOrder: 0);
        var edge = NewProduct(iphone, "Edge Phone", "edge", 0m, sortOrder: 1);
        var split = NewProduct(iphone, "Split Phone", "split", 0m, sortOrder: 2);
        var retired = NewProduct(iphone, "Retired Phone", "retired", 0m, sortOrder: 3);
        var contact = NewProduct(iphone, "Contact Phone", "contact", 0m, sortOrder: 4);
        var laptop = NewProduct(mac, "Split Laptop", "laptop", 0m, sortOrder: 5);
        fixture.Context.AddRange(cheap, edge, split, retired, contact, laptop);
        AddVariant(fixture, cheap, "C", 6_490_000m, 1);
        AddVariant(fixture, edge, "E", 10_000_000m, 1);
        AddVariant(fixture, split, "S1", 25_000_000m, 1);
        AddVariant(fixture, split, "S2", 45_000_000m, 1);
        AddVariant(fixture, retired, "R1", 5_000_000m, 1, status: false);
        AddVariant(fixture, retired, "R2", 30_000_000m, 1);
        AddVariant(fixture, contact, "N", null, 1);
        AddVariant(fixture, laptop, "L", 39_999_999m, 1);
        await fixture.Context.SaveChangesAsync();
        return (sut, fixture);
    }

    [Theory]
    [InlineData(PriceBand.Under10M, "cheap")]
    [InlineData(PriceBand.From10MTo20M, "edge")]
    [InlineData(PriceBand.From20MTo40M, "split,retired,laptop")]
    [InlineData(PriceBand.Over40M, "split")]
    public async Task GetProductsAsync_keeps_products_with_an_active_variant_in_the_band(PriceBand band, string expected)
    {
        var (sut, fixture) = await BandShopAsync();
        using var _ = fixture;

        var result = await sut.GetProductsAsync(band: band);

        Assert.Equal(expected.Split(','), result.Select(p => p.Slug));
    }

    [Fact]
    public async Task GetProductsAsync_in_a_band_shows_the_lowest_price_inside_it()
    {
        var (sut, fixture) = await BandShopAsync();
        using var _ = fixture;

        var over = await sut.GetProductsAsync(band: PriceBand.Over40M);
        var any = await sut.GetProductsAsync();

        Assert.Equal(45_000_000m, Assert.Single(over).FromPrice);
        Assert.Equal(25_000_000m, any.Single(p => p.Slug == "split").FromPrice);
    }

    [Fact]
    public async Task GetProductsAsync_without_a_band_still_lists_unpriced_products()
    {
        var (sut, fixture) = await BandShopAsync();
        using var _ = fixture;

        Assert.Contains("contact", (await sut.GetProductsAsync()).Select(p => p.Slug));
        Assert.DoesNotContain("contact", (await sut.GetProductsAsync(band: PriceBand.Under10M)).Select(p => p.Slug));
    }

    [Fact]
    public async Task GetProductsAsync_band_combines_with_category_query_and_price_sort()
    {
        var (sut, fixture) = await BandShopAsync();
        using var _ = fixture;

        var phones = await sut.GetProductsAsync(categorySlug: "iphone", band: PriceBand.From20MTo40M, sort: ProductSort.PriceDescending);
        var searched = await sut.GetProductsAsync(query: "laptop", band: PriceBand.From20MTo40M);

        Assert.Equal(new[] { "retired", "split" }, phones.Select(p => p.Slug));
        Assert.Equal(new[] { "laptop" }, searched.Select(p => p.Slug));
    }

    [Fact]
    public async Task GetProductsAsync_an_unknown_band_value_means_no_filter()
    {
        var (sut, fixture) = await BandShopAsync();
        using var _ = fixture;

        var result = await sut.GetProductsAsync(band: (PriceBand)99);

        Assert.Equal(6, result.Count);
    }

    [Fact]
    public async Task GetProductsAsync_newest_puts_the_latest_added_first()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var old = NewProduct(iphone, "Old", "old", 0m, sortOrder: 0);
        var latest = NewProduct(iphone, "Latest", "latest", 0m, sortOrder: 2);
        var middle = NewProduct(iphone, "Middle", "middle", 0m, sortOrder: 1);
        var sameDayLater = NewProduct(iphone, "Same day later", "same-day-later", 0m, sortOrder: 3);
        old.CreatedAt = new DateTime(2026, 1, 1);
        latest.CreatedAt = new DateTime(2026, 9, 1);
        middle.CreatedAt = new DateTime(2026, 5, 1);
        sameDayLater.CreatedAt = new DateTime(2026, 5, 1);
        fixture.Context.AddRange(old, latest, middle, sameDayLater);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync(sort: ProductSort.Newest);

        // A tie on CreatedAt goes to the one added later (higher Id).
        Assert.Equal(new[] { "latest", "same-day-later", "middle", "old" }, result.Select(p => p.Slug));
    }

    // rauvang.com's model page: one card per configuration, in the order the
    // store lists them, each "From" its cheapest colour.
    [Fact]
    public async Task GetBySlugAsync_groups_variants_into_configurations_in_catalog_order()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var product = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 18 Pro Max", "iphone-18-pro-max", 0m);
        product.Description = "desc";
        fixture.Context.Add(product);
        fixture.Context.Add(new ProductImage { Product = product, ImageUrl = "/img/products/iphone-18-pro-max.jpg", SortOrder = 0 });
        AddVariant(fixture, product, "256-BLACK", 42_300_000m, 0, "iPhone 18 Pro Max 256GB ( VN )", "Black", "VN");
        AddVariant(fixture, product, "256-SILVER", 43_600_000m, 5, "iPhone 18 Pro Max 256GB ( VN )", "Silver", "VN");
        AddVariant(fixture, product, "2TB-BLACK", null, 3, "iPhone 18 Pro Max 2TB ( Mỹ )", "Black", "Mỹ");
        AddVariant(fixture, product, "512-OLD", 1m, 3, "iPhone 18 Pro Max 512GB ( VN )", "Black", "VN", status: false);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetBySlugAsync("iphone-18-pro-max");

        Assert.NotNull(result);
        Assert.Equal("iphone", result!.CategorySlug);
        Assert.Single(result.ImageUrls);
        Assert.Collection(result.Configurations,
            c =>
            {
                Assert.Equal("iPhone 18 Pro Max 256GB ( VN )", c.Name);
                Assert.Equal("iphone-18-pro-max-256gb-vn", c.Slug);
                Assert.Equal(42_300_000m, c.FromPrice);
                Assert.True(c.InStock);
            },
            c =>
            {
                Assert.Equal("iphone-18-pro-max-2tb-my", c.Slug);
                Assert.Null(c.FromPrice);
            });
        Assert.Equal(42_300_000m, result.FromPrice);
    }

    // Products sold as a single item (AirPods, accessories) carry no "config"
    // option; they form one configuration named after the product.
    [Fact]
    public async Task GetBySlugAsync_variants_without_a_config_form_one_configuration_named_after_the_product()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var product = NewProduct(NewCategory("AirPods", "airpods"), "AirPods Max 2", "airpods-max-2", 0m);
        fixture.Context.Add(product);
        AddVariant(fixture, product, "MAX2-MIDNIGHT", 11_600_000m, 4, color: "Midnight");
        AddVariant(fixture, product, "MAX2-BLUE", 11_600_000m, 0, color: "Blue");
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetBySlugAsync("airpods-max-2");

        var config = Assert.Single(result!.Configurations);
        Assert.Equal("AirPods Max 2", config.Name);
        Assert.Equal("airpods-max-2", config.Slug);
    }

    [Fact]
    public async Task GetBySlugAsync_returns_null_for_unknown_slug()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;

        var result = await sut.GetBySlugAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_returns_null_for_inactive_product()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        fixture.Context.Add(NewProduct(NewCategory("Mac", "mac"), "Discontinued Mac", "discontinued-mac", 899m, status: false));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetBySlugAsync("discontinued-mac");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetConfigurationAsync_returns_the_colour_and_region_choices_with_product_context()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var product = NewProduct(NewCategory("iPad", "ipad"), "iPad Mini 7", "ipad-mini-7", 0m);
        fixture.Context.Add(product);
        fixture.Context.Add(new ProductImage { Product = product, ImageUrl = "/img/products/ipad-mini.jpg", SortOrder = 0 });
        AddVariant(fixture, product, "128-BLUE-VN", 13_600_000m, 7, "iPad Mini Gen 7 WIFI - 128GB", "Blue", "VN");
        AddVariant(fixture, product, "128-BLUE-US", 13_600_000m, 2, "iPad Mini Gen 7 WIFI - 128GB", "Blue", "Mỹ");
        AddVariant(fixture, product, "256-GRAY-US", 16_900_000m, 2, "iPad Mini Gen 7 WIFI - 256GB", "Space Gray", "Mỹ");
        AddVariant(fixture, product, "128-OLD", 1m, 2, "iPad Mini Gen 7 WIFI - 128GB", "Purple", "VN", status: false);
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetConfigurationAsync("ipad-mini-7", "ipad-mini-gen-7-wifi-128gb");

        Assert.NotNull(result);
        Assert.Equal("iPad Mini Gen 7 WIFI - 128GB", result!.Name);
        Assert.Equal("iPad Mini 7", result.ProductName);
        Assert.Equal("ipad", result.CategorySlug);
        Assert.Equal("/img/products/ipad-mini.jpg", result.ImageUrl);
        Assert.Equal(new[] { "128-BLUE-VN", "128-BLUE-US" }, result.Choices.Select(c => c.SKU));
        Assert.Equal(("Blue", "Mỹ", 13_600_000m, 2), (result.Choices[1].Color, result.Choices[1].Region, result.Choices[1].Price, result.Choices[1].StockQty));
    }

    [Fact]
    public async Task GetConfigurationAsync_returns_null_for_an_unknown_configuration()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var product = NewProduct(NewCategory("iPad", "ipad"), "iPad Mini 7", "ipad-mini-7", 0m);
        fixture.Context.Add(product);
        AddVariant(fixture, product, "128-BLUE-VN", 13_600_000m, 7, "iPad Mini Gen 7 WIFI - 128GB", "Blue", "VN");
        await fixture.Context.SaveChangesAsync();

        Assert.Null(await sut.GetConfigurationAsync("ipad-mini-7", "does-not-exist"));
    }

    // A real configuration slug under another product's URL must 404, not
    // quietly serve that configuration under the wrong product.
    [Fact]
    public async Task GetConfigurationAsync_returns_null_when_the_configuration_belongs_to_another_product()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var ipad = NewCategory("iPad", "ipad");
        var mini = NewProduct(ipad, "iPad Mini 7", "ipad-mini-7", 0m);
        var air = NewProduct(ipad, "iPad Air 8 11\"", "ipad-air-8-11", 0m);
        fixture.Context.AddRange(mini, air);
        AddVariant(fixture, mini, "128-BLUE-VN", 13_600_000m, 7, "iPad Mini Gen 7 WIFI - 128GB", "Blue", "VN");
        await fixture.Context.SaveChangesAsync();

        Assert.Null(await sut.GetConfigurationAsync("ipad-air-8-11", "ipad-mini-gen-7-wifi-128gb"));
    }

    [Fact]
    public async Task GetConfigurationAsync_returns_null_for_an_inactive_product()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var product = NewProduct(NewCategory("Mac", "mac"), "Old Mac", "old-mac", 0m, status: false);
        fixture.Context.Add(product);
        AddVariant(fixture, product, "OLD", 1m, 1, "Old Mac 256GB");
        await fixture.Context.SaveChangesAsync();

        Assert.Null(await sut.GetConfigurationAsync("old-mac", "old-mac-256gb"));
    }
}
