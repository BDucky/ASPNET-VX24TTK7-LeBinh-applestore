using System.Net;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// The compare list over HTTP. It lives in a cookie (owner's decision
// 2026-10-08), so a visitor without an account can use it.
public class CompareFlowTests : WebFlowTestBase
{
    private const string PhonePage = "/Products/iphone-17";

    private (int Phone, int Pods, int Watch, int Mac, int Hidden) SeedShop()
    {
        Seed();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods Pro 3", "airpods-pro-3", 1m);
        AddVariant(db, pods, "A1", 6_490_000m, 2);
        var watch = NewProduct(NewCategory("Watch", "watch"), "Apple Watch 12", "watch-12", 1m);
        AddVariant(db, watch, "W1", 10_990_000m, 1);
        var mac = NewProduct(NewCategory("Mac", "mac"), "Mac mini M4", "mac-mini-m4", 1m);
        AddVariant(db, mac, "M1", 14_990_000m, 1);
        var hidden = NewProduct(NewCategory("Old", "old"), "iPhone 15", "iphone-15", 1m, status: false);
        db.AddRange(pods, watch, mac, hidden);
        db.SaveChanges();
        var phone = db.Products.Single(p => p.Slug == "iphone-17").Id;
        return (phone, pods.Id, watch.Id, mac.Id, hidden.Id);
    }

    private Task<HttpResponseMessage> AddAsync(int productId, string from = PhonePage) =>
        PostFormAsync("/Compare/Add", new() { ["ProductId"] = productId.ToString(), ["ReturnUrl"] = from }, formPage: PhonePage);

    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    [Fact]
    public async Task A_visitor_adds_two_products_and_sees_them_side_by_side()
    {
        var (phone, pods, _, _, _) = SeedShop();

        var first = await AddAsync(phone);
        await AddAsync(pods, "/Products/airpods-pro-3");

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(PhonePage, first.Headers.Location!.OriginalString);
        var page = await PageAsync("/Compare");
        Assert.True(page.IndexOf("iPhone 17", StringComparison.Ordinal) < page.IndexOf("AirPods Pro 3", StringComparison.Ordinal));
        Assert.Contains("24.990.000 VNĐ", page);
        Assert.Contains("Compare (2)", await PageAsync("/"));
    }

    [Fact]
    public async Task The_product_page_offers_the_compare_button_to_a_visitor()
    {
        SeedShop();

        var page = await PageAsync(PhonePage);
        var variant = await PageAsync(PhoneVariantUrl);

        Assert.Contains("action=\"/Compare/Add\"", page);
        Assert.Contains("action=\"/Compare/Add\"", variant);
    }

    [Fact]
    public async Task The_added_message_shows_once_on_the_page_the_visitor_came_from()
    {
        var (phone, _, _, _, _) = SeedShop();
        await AddAsync(phone);

        Assert.Contains("Added to compare.", await PageAsync(PhonePage));
        Assert.DoesNotContain("Added to compare.", await PageAsync(PhonePage));
    }

    [Fact]
    public async Task A_fourth_product_is_refused_with_a_message_and_the_three_stay()
    {
        var (phone, pods, watch, mac, _) = SeedShop();
        await AddAsync(phone);
        await AddAsync(pods);
        await AddAsync(watch);

        await AddAsync(mac);

        Assert.Contains("You can compare up to 3 products. Remove one first.", await PageAsync(PhonePage));
        var page = await PageAsync("/Compare");
        Assert.DoesNotContain("Mac mini M4", page);
        Assert.Contains("Apple Watch 12", page);
    }

    [Fact]
    public async Task A_hidden_product_cannot_be_added()
    {
        var (_, _, _, _, hidden) = SeedShop();

        await AddAsync(hidden);

        Assert.Contains("That product could not be found.", await PageAsync(PhonePage));
        Assert.DoesNotContain("iPhone 15", await PageAsync("/Compare"));
    }

    [Fact]
    public async Task Remove_takes_one_product_out_and_clear_empties_the_list()
    {
        var (phone, pods, _, _, _) = SeedShop();
        await AddAsync(phone);
        await AddAsync(pods);

        var removed = await PostFormAsync("/Compare/Remove", new() { ["ProductId"] = phone.ToString() }, formPage: "/Compare");
        var afterRemove = await PageAsync("/Compare");
        await PostFormAsync("/Compare/Clear", new(), formPage: "/Compare");
        var afterClear = await PageAsync("/Compare");

        Assert.Equal("/Compare", removed.Headers.Location!.OriginalString);
        Assert.DoesNotContain("iPhone 17", afterRemove);
        Assert.Contains("AirPods Pro 3", afterRemove);
        Assert.DoesNotContain("AirPods Pro 3", afterClear);
        Assert.Contains("Nothing to compare yet", afterClear);
        Assert.DoesNotContain("Compare (", await PageAsync("/"));
    }

    // A hand-edited cookie must not break the page or slip in a fourth column.
    [Fact]
    public async Task A_tampered_cookie_shows_only_the_first_three_real_products()
    {
        var (phone, pods, watch, mac, hidden) = SeedShop();
        var request = new HttpRequestMessage(HttpMethod.Get, "/Compare");
        request.Headers.Add("Cookie", $"AppleStore.Compare=abc.-1.{hidden}.{phone}.{phone}.{pods}.{watch}.{mac}.99999999999");

        var response = await Client.SendAsync(request);
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Apple Watch 12", page);
        Assert.DoesNotContain("Mac mini M4", page);
        Assert.DoesNotContain("iPhone 15", page);
        // The cookie is cut down to what the page shows, and the nav on this
        // same page already counts that (found in review 2026-10-08).
        Assert.Contains($"AppleStore.Compare={phone}.{pods}.{watch};", string.Join("\n", response.Headers.GetValues("Set-Cookie")));
        Assert.Contains("Compare (3)", page);
    }

    [Fact]
    public async Task The_nav_never_counts_more_than_the_cap_from_a_tampered_cookie()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", "AppleStore.Compare=" + string.Join('.', Enumerable.Range(1, 50)));

        var page = await (await Client.SendAsync(request)).Content.ReadAsStringAsync();

        Assert.Contains("Compare (3)", page);
    }

    [Fact]
    public async Task Add_without_the_form_token_is_rejected()
    {
        var (phone, _, _, _, _) = SeedShop();

        var response = await Client.PostAsync("/Compare/Add", new FormUrlEncodedContent(new Dictionary<string, string> { ["ProductId"] = phone.ToString() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    public async Task Add_ignores_a_return_address_on_another_site(string returnUrl)
    {
        var (phone, _, _, _, _) = SeedShop();

        var response = await AddAsync(phone, returnUrl);

        Assert.Equal("/Compare", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task The_empty_page_points_back_to_the_shop()
    {
        var page = await PageAsync("/Compare");

        Assert.Contains("Nothing to compare yet", page);
        Assert.Contains("href=\"/Products\"", page);
    }
}

// The database failing under the compare page: a message and a way on, and
// the visitor's list is kept, not wiped.
public class CompareFailureTests : WebFlowTestBase
{
    public CompareFailureTests() : base(new AppleStoreWebFactory(configureServices: services =>
    {
        services.RemoveAll<ICompareService>();
        services.AddScoped<ICompareService, FailingCompareService>();
    }))
    {
    }

    private async Task<HttpResponseMessage> WithListAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("Cookie", "AppleStore.Compare=1.2");
        return await Client.SendAsync(request);
    }

    [Fact]
    public async Task A_failed_page_says_so_and_keeps_the_list()
    {
        var response = await WithListAsync(HttpMethod.Get, "/Compare");
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("We could not load your compare list. Please try again.", page);
        Assert.False(response.Headers.Contains("Set-Cookie") && response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("AppleStore.Compare")));
    }

    [Fact]
    public async Task A_failed_add_says_so_on_the_page_and_keeps_the_list()
    {
        var response = await PostFormAsync("/Compare/Add", new() { ["ProductId"] = "3", ["ReturnUrl"] = "/Products" }, formPage: "/Account/Login");

        Assert.Equal("/Products", response.Headers.Location?.OriginalString);
        Assert.False(response.Headers.Contains("Set-Cookie") && response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("AppleStore.Compare")));
        Assert.Contains("We could not update your compare list. Please try again.", WebUtility.HtmlDecode(await Client.GetStringAsync("/Products")));
    }

    private sealed class FailingCompareService : ICompareService
    {
        private static DbUpdateException Failure() => new("database is locked");

        public Task<IReadOnlyList<CompareColumn>> BuildAsync(IReadOnlyList<int> productIds, CancellationToken ct = default) => throw Failure();
        public Task<CompareResult> AddAsync(IReadOnlyList<int> productIds, int productId, CancellationToken ct = default) => throw Failure();
    }
}
