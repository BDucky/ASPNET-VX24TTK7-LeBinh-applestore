using System.Net;
using AppleStore.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// Five products have no licensed photo of their own model yet. The owner
// chose a placeholder over an older model's photo (2026-10-10); it shows
// the kind of product, not always a phone.
public class PhotoPlaceholderTests : WebFlowTestBase
{
    private void SeedWithoutPhotos()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (category, slug, name) in new[]
        {
            ("watch", "apple-watch-se-3", "Apple Watch SE 3"),
            ("ipad", "ipad-air-8-11", "iPad Air 8 11\""),
            ("accessories", "magsafe-battery-pack", "MagSafe Battery Pack"),
        })
        {
            var product = NewProduct(NewCategory(category, category), name, slug, 1m);
            AddVariant(db, product, slug.ToUpperInvariant(), 9_990_000m, 3, config: name);
            db.Add(product);
        }
        db.SaveChanges();
    }

    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    [Theory]
    [InlineData("/Products?category=watch", "watch")]
    [InlineData("/Products?q=Watch", "watch")]
    [InlineData("/Products/ipad-air-8-11", "tablet")]
    [InlineData("/Products/magsafe-battery-pack", "accessory")]
    public async Task A_product_without_a_photo_shows_its_own_kind_of_placeholder(string url, string kind)
    {
        SeedWithoutPhotos();

        var page = await PageAsync(url);

        Assert.Contains($"data-no-photo=\"{kind}\"", page);
        Assert.DoesNotContain("data-no-photo=\"phone\"", page);
    }

    [Fact]
    public async Task The_configuration_page_shows_the_placeholder_of_its_kind()
    {
        SeedWithoutPhotos();
        var detail = await PageAsync("/Products/apple-watch-se-3");
        var config = System.Text.RegularExpressions.Regex.Match(detail, "href=\"(/Products/apple-watch-se-3/[^\"]+)\"").Groups[1].Value;

        Assert.Contains("data-no-photo=\"watch\"", await PageAsync(config));
    }
}
