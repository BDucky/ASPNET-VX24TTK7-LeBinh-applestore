using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Tests;

public class HomeControllerTests
{
    [Fact]
    public async Task Index_builds_carousel_and_banners_from_the_curated_slugs()
    {
        var detail = new ProductDetail(1, "iPhone 18 Pro", "iphone-18-pro", "desc", "iPhone", "iphone", Array.Empty<ProductVariantSummary>(), Array.Empty<string>());
        var sut = new HomeController(new FakeProductCatalogService { DetailToReturn = detail });

        var result = await sut.Index(default);

        var model = Assert.IsType<HomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(HomeViewModel.CarouselSlugs.Count, model.Carousel.Count);
        Assert.Equal(HomeViewModel.BannerSlugs.Count, model.Banners.Count);
    }

    // A deactivated or renamed product drops its banner, it never breaks the page.
    [Fact]
    public async Task Index_skips_slugs_the_catalog_cannot_find()
    {
        var sut = new HomeController(new FakeProductCatalogService { DetailToReturn = null });

        var result = await sut.Index(default);

        var model = Assert.IsType<HomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Carousel);
        Assert.Empty(model.Banners);
    }
}
