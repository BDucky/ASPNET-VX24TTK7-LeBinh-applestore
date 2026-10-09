using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// Use case 8 over HTTP: price bands and the newest sort on the product list,
// and every filter surviving the others being changed.
public class CatalogFilterFlowTests : WebFlowTestBase
{
    private void SeedShop()
    {
        Seed(); // iPhone 17: 24.990.000 and 25.490.000
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods 4", "airpods-4", 1m);
        AddVariant(db, pods, "A1", 3_490_000m, 2);
        var mac = NewProduct(NewCategory("Mac", "mac"), "MacBook Pro", "macbook-pro", 1m);
        AddVariant(db, mac, "M1", 39_990_000m, 1);
        AddVariant(db, mac, "M2", 64_990_000m, 1);
        db.AddRange(pods, mac);
        db.SaveChanges();
    }

    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    private static List<string> CardNames(string html) =>
        Regex.Matches(html, "<h3>([^<]+)</h3>").Select(m => m.Groups[1].Value).ToList();

    private static string BandHref(string html, string band) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"href=\"([^\"]+)\"[^>]*data-price-band=\"{band}\"").Groups[1].Value);

    [Fact]
    public async Task A_band_lists_only_products_priced_inside_it_with_that_price()
    {
        SeedShop();

        var page = await PageAsync("/Products?band=Over40M");

        Assert.Equal(["MacBook Pro"], CardNames(page));
        Assert.Contains("64.990.000 VNĐ", page);
        Assert.DoesNotContain("39.990.000 VNĐ", page);
    }

    [Fact]
    public async Task The_band_links_keep_the_search_category_and_sort()
    {
        SeedShop();

        var page = await PageAsync("/Products?q=phone&category=iphone&sort=PriceAscending");
        var href = BandHref(page, "From20MTo40M");

        Assert.StartsWith("/Products?", href);
        Assert.Contains("band=From20MTo40M", href);
        Assert.Contains("q=phone", href);
        Assert.Contains("category=iphone", href);
        Assert.Contains("sort=PriceAscending", href);
    }

    [Fact]
    public async Task The_sort_and_search_forms_keep_the_band()
    {
        SeedShop();

        var page = await PageAsync("/Products?band=From20MTo40M");

        var sortForm = Regex.Match(page, "<form[^>]*class=\"sort-form\"[\\s\\S]*?</form>").Value;
        var searchForm = Regex.Match(page, "<form[^>]*class=\"product-search\"[\\s\\S]*?</form>").Value;
        Assert.Contains("name=\"band\" value=\"From20MTo40M\"", sortForm);
        Assert.Contains("name=\"band\" value=\"From20MTo40M\"", searchForm);
        Assert.Contains("value=\"Newest\"", sortForm);
    }

    [Fact]
    public async Task The_chosen_band_is_marked_and_all_prices_clears_it()
    {
        SeedShop();

        var page = await PageAsync("/Products?band=Under10M&q=a");

        Assert.Matches("aria-current=\"true\"[^>]*data-price-band=\"Under10M\"|data-price-band=\"Under10M\"[^>]*aria-current=\"true\"", page);
        var all = BandHref(page, "Any");
        Assert.DoesNotContain("band=", all);
        Assert.Contains("q=a", all);
    }

    [Fact]
    public async Task A_category_with_a_band_shows_the_filtered_grid_not_the_landing_strip()
    {
        SeedShop();

        var page = await PageAsync("/Products?category=mac&band=From20MTo40M");

        Assert.Equal(["MacBook Pro"], CardNames(page));
        Assert.Contains("39.990.000 VNĐ", page);
        Assert.Contains("data-price-band=\"Over40M\"", page);
    }

    [Theory]
    [InlineData("junk")]
    [InlineData("99")]
    [InlineData("-1")]
    public async Task A_hand_edited_band_lists_everything_instead_of_failing(string band)
    {
        SeedShop();

        var response = await Client.GetAsync($"/Products?band={band}");
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, CardNames(page).Count);
    }

    [Fact]
    public async Task An_empty_band_says_so_and_offers_all_prices()
    {
        SeedShop();

        var page = await PageAsync("/Products?category=airpods&band=Over40M");

        Assert.Contains("No products match", page);
        Assert.Contains("data-price-band=\"Any\"", page);
    }
}
