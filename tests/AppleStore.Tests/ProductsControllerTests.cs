using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models.Products;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Tests;

public class ProductsControllerTests
{
    [Fact]
    public async Task Index_returns_view_with_products_from_the_catalog()
    {
        var catalog = new FakeProductCatalogService
        {
            ProductsToReturn = new[] { new ProductSummary(1, "iPhone 17", "iphone-17", "iPhone", "iphone", 999m, "/img/products/iphone-17.jpg") },
        };
        var sut = new ProductsController(catalog);

        var result = await sut.Index(category: null, q: null, ct: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProductListViewModel>(view.Model);
        Assert.Single(model.Products);
    }

    [Fact]
    public async Task Index_passes_category_and_query_through_to_the_catalog_and_view()
    {
        var catalog = new FakeProductCatalogService();
        var sut = new ProductsController(catalog);

        var result = await sut.Index(category: "mac", q: "pro", ct: default);

        Assert.Equal("mac", catalog.LastCategorySlug);
        Assert.Equal("pro", catalog.LastQuery);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProductListViewModel>(view.Model);
        Assert.Equal("mac", model.SelectedCategory);
        Assert.Equal("pro", model.Query);
    }

    [Fact]
    public async Task Details_returns_view_with_product_when_found()
    {
        var detail = new ProductDetail(1, "iPhone 17", "iphone-17", "desc", "iPhone", Array.Empty<ProductVariantSummary>(), Array.Empty<string>());
        var catalog = new FakeProductCatalogService { DetailToReturn = detail };
        var sut = new ProductsController(catalog);

        var result = await sut.Details("iphone-17", ct: default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(detail, view.Model);
        Assert.Equal("iphone-17", catalog.LastSlugRequested);
    }

    [Fact]
    public async Task Details_returns_not_found_for_unknown_slug()
    {
        var catalog = new FakeProductCatalogService { DetailToReturn = null };
        var sut = new ProductsController(catalog);

        var result = await sut.Details("does-not-exist", ct: default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Variant_returns_view_with_variant_when_found()
    {
        var variant = new ProductVariantDetail(1, "IP17-128", 999m, 10, 1, "iPhone 17", "iphone-17", "iPhone", "/img/products/iphone-17.jpg");
        var catalog = new FakeProductCatalogService { VariantToReturn = variant };
        var sut = new ProductsController(catalog);

        var result = await sut.Variant("iphone-17", "IP17-128", ct: default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(variant, view.Model);
        Assert.Equal("iphone-17", catalog.LastVariantProductSlugRequested);
        Assert.Equal("IP17-128", catalog.LastVariantSkuRequested);
    }

    [Fact]
    public async Task Variant_returns_not_found_when_catalog_returns_null()
    {
        var catalog = new FakeProductCatalogService { VariantToReturn = null };
        var sut = new ProductsController(catalog);

        var result = await sut.Variant("iphone-17", "does-not-exist", ct: default);

        Assert.IsType<NotFoundResult>(result);
    }
}
