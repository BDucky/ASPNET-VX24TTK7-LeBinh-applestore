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

    private static Category NewCategory(string name, string slug) =>
        new() { Name = name, Slug = slug };

    private static Product NewProduct(Category category, string name, string slug, decimal basePrice, bool status = true) =>
        new()
        {
            Category = category,
            Name = name,
            Slug = slug,
            BasePrice = basePrice,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static ProductVariant NewVariant(Product product, string sku, decimal price, int stock, bool status = true) =>
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
    public async Task GetProductsAsync_sorts_by_price_ascending()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        fixture.Context.AddRange(
            NewProduct(iphone, "Expensive", "expensive", 1999m),
            NewProduct(iphone, "Cheap", "cheap", 299m));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync(sort: ProductSort.PriceAscending);

        Assert.Equal("cheap", result[0].Slug);
        Assert.Equal("expensive", result[1].Slug);
    }

    [Fact]
    public async Task GetProductsAsync_sorts_by_price_descending()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        fixture.Context.AddRange(
            NewProduct(iphone, "Cheap", "cheap", 299m),
            NewProduct(iphone, "Expensive", "expensive", 1999m));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetProductsAsync(sort: ProductSort.PriceDescending);

        Assert.Equal("expensive", result[0].Slug);
        Assert.Equal("cheap", result[1].Slug);
    }

    [Fact]
    public async Task GetBySlugAsync_returns_product_with_variants_and_images()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var product = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        product.Description = "The latest iPhone.";
        fixture.Context.Add(product);
        fixture.Context.Add(NewVariant(product, "IP17-128", 999m, 10));
        fixture.Context.Add(new ProductImage { Product = product, ImageUrl = "/img/iphone-17.svg", SortOrder = 0 });
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetBySlugAsync("iphone-17");

        Assert.NotNull(result);
        Assert.Equal("iPhone 17", result!.Name);
        Assert.Equal("The latest iPhone.", result.Description);
        Assert.Single(result.Variants);
        Assert.Single(result.ImageUrls);
        Assert.Equal("iphone", result.CategorySlug);
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
        var mac = NewCategory("Mac", "mac");
        fixture.Context.Add(NewProduct(mac, "Discontinued Mac", "discontinued-mac", 899m, status: false));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetBySlugAsync("discontinued-mac");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetVariantAsync_returns_variant_with_product_context_when_found()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var product = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        fixture.Context.Add(product);
        fixture.Context.Add(NewVariant(product, "IP17-128", 999m, 10));
        fixture.Context.Add(new ProductImage { Product = product, ImageUrl = "/img/products/iphone-17.jpg", SortOrder = 0 });
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetVariantAsync("iphone-17", "IP17-128");

        Assert.NotNull(result);
        Assert.Equal("IP17-128", result!.SKU);
        Assert.Equal(999m, result.Price);
        Assert.Equal("iPhone 17", result.ProductName);
        Assert.Equal("iphone-17", result.ProductSlug);
        Assert.Equal("iPhone", result.CategoryName);
        Assert.Equal("iphone", result.CategorySlug);
        Assert.Equal("/img/products/iphone-17.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task GetVariantAsync_returns_null_for_unknown_sku()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var product = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        fixture.Context.Add(product);
        fixture.Context.Add(NewVariant(product, "IP17-128", 999m, 10));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetVariantAsync("iphone-17", "DOES-NOT-EXIST");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetVariantAsync_returns_null_when_sku_belongs_to_a_different_product()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var realProduct = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        var otherProduct = NewProduct(iphone, "iPhone 17 Pro", "iphone-17-pro", 1199m);
        fixture.Context.AddRange(realProduct, otherProduct);
        fixture.Context.Add(NewVariant(realProduct, "IP17-128", 999m, 10));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetVariantAsync("iphone-17-pro", "IP17-128");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetVariantAsync_returns_null_for_inactive_variant()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var iphone = NewCategory("iPhone", "iphone");
        var product = NewProduct(iphone, "iPhone 17", "iphone-17", 999m);
        fixture.Context.Add(product);
        fixture.Context.Add(NewVariant(product, "IP17-512-DISC", 899m, 0, status: false));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetVariantAsync("iphone-17", "IP17-512-DISC");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetVariantAsync_returns_null_for_inactive_product()
    {
        var (sut, fixture) = CreateSut();
        using var _ = fixture;
        var mac = NewCategory("Mac", "mac");
        var product = NewProduct(mac, "Discontinued Mac", "discontinued-mac", 899m, status: false);
        fixture.Context.Add(product);
        fixture.Context.Add(NewVariant(product, "DISC-MAC-256", 899m, 5));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.GetVariantAsync("discontinued-mac", "DISC-MAC-256");

        Assert.Null(result);
    }
}
