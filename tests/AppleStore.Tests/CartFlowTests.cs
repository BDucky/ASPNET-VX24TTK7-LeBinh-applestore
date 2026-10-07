using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// The cart through the real app: [Authorize], anti-forgery, the product
// page's form, the cart page and the nav count, with the real CartService.
public class CartFlowTests : WebFlowTestBase
{
    private const string Password = "Password1";
    private const string VariantUrl = "/Products/iphone-17/iphone-17-256gb";
    private const string Email = "shopper@example.com";

    private (int Blue, int Black, int SoldOut) Seed()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 20_000_000m);
        var blue = AddVariant(db, product, "IP17-256-BLUE", 24_990_000m, stock: 3, config: "iPhone 17 256GB", color: "Blue", region: "VN/A");
        var black = AddVariant(db, product, "IP17-256-BLACK", 25_490_000m, stock: 5, config: "iPhone 17 256GB", color: "Black", region: "VN/A");
        var soldOut = AddVariant(db, product, "IP17-256-PINK", 24_990_000m, stock: 0, config: "iPhone 17 256GB", color: "Pink", region: "VN/A");
        db.SaveChanges();
        return (blue.Id, black.Id, soldOut.Id);
    }

    private async Task SignInAsync(string email = Email)
    {
        await CreateUserAsync(email, Password);
        await LoginAsync(email, Password);
    }

    private Task<HttpResponseMessage> AddAsync(int variantId, string quantity = "1", string returnUrl = VariantUrl) =>
        PostFormAsync("/Cart/Add", new() { ["VariantId"] = variantId.ToString(), ["Quantity"] = quantity, ["ReturnUrl"] = returnUrl }, formPage: VariantUrl);

    // Razor encodes the "Đ" of "VNĐ"; pages are compared as the shopper reads them.
    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    private static string Location(HttpResponseMessage response) =>
        response.Headers.Location?.OriginalString ?? "";

    private static string Notice(string html, string kind) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"class=\"cart-{kind}\"[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value);

    private async Task<int> LineIdAsync(int variantId)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().CartItems
            .Where(i => i.VariantId == variantId).Select(i => i.Id).SingleAsync();
    }

    private async Task<List<(string Email, int VariantId, int Quantity)>> StoredAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().CartItems
                .OrderBy(i => i.Id).Select(i => new { i.Cart.User.Email, i.VariantId, i.Quantity }).ToListAsync())
            .Select(i => (i.Email, i.VariantId, i.Quantity)).ToList();
    }

    // ---------- Signed out ----------

    [Fact]
    public async Task A_visitor_sees_a_sign_in_link_that_comes_back_to_the_product_not_a_form()
    {
        Seed();

        var html = await Client.GetStringAsync(VariantUrl + "?color=Black");

        Assert.DoesNotContain("action=\"/Cart/Add\"", html);
        Assert.Contains("href=\"/Account/Login?returnUrl=%2FProducts%2Fiphone-17%2Fiphone-17-256gb%3Fcolor%3DBlack\"", html);
    }

    [Theory]
    [InlineData("/Cart")]
    public async Task A_visitor_opening_the_cart_is_sent_to_sign_in(string path)
    {
        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login?ReturnUrl=%2FCart", response.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task Signing_in_from_the_product_page_link_returns_to_that_product()
    {
        Seed();
        await CreateUserAsync(Email, Password);

        var login = await LoginAsync(Email, Password, returnUrl: VariantUrl + "?color=Black");

        Assert.Equal(VariantUrl + "?color=Black", Location(login));
    }

    [Fact]
    public async Task Landing_on_the_add_address_after_sign_in_goes_to_the_cart_not_an_error()
    {
        await SignInAsync();

        var response = await Client.GetAsync("/Cart/Add");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Cart", Location(response));
    }

    // ---------- Adding ----------

    [Fact]
    public async Task The_product_page_form_posts_the_selected_variant()
    {
        var (_, black, _) = Seed();
        await SignInAsync();

        var html = await Client.GetStringAsync(VariantUrl + "?color=Black");

        Assert.Contains("action=\"/Cart/Add\"", html);
        Assert.Equal(black.ToString(), Regex.Match(html, "name=\"VariantId\"[^>]*value=\"(\\d+)\"").Groups[1].Value);
    }

    [Fact]
    public async Task Adding_goes_to_the_cart_which_shows_the_line_and_the_nav_count()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();

        var response = await AddAsync(blue, "2");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Cart", Location(response));
        var cart = await PageAsync("/Cart");
        Assert.Equal("Added to your cart.", Notice(cart, "status"));
        Assert.Contains("iPhone 17", cart);
        Assert.Contains("Blue", cart);
        Assert.Contains("49.980.000 VNĐ", cart);
        Assert.Matches("data-cart-count[^>]*>\\s*2\\s*<", cart);
        Assert.Equal([(Email, blue, 2)], await StoredAsync());
    }

    [Fact]
    public async Task A_refused_add_goes_back_to_the_product_page_and_says_why()
    {
        var (_, _, soldOut) = Seed();
        await SignInAsync();

        var response = await AddAsync(soldOut);

        Assert.Equal(VariantUrl, Location(response));
        var page = await Client.GetStringAsync(VariantUrl);
        Assert.Equal("This product is out of stock.", Notice(page, "error"));
        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task Adding_past_the_stock_says_how_many_more_fit()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddAsync(blue, "2");

        await AddAsync(blue, "2");

        Assert.Equal("Only 1 more can be added: that is all the stock we have.", Notice(await Client.GetStringAsync(VariantUrl), "error"));
        Assert.Equal([(Email, blue, 2)], await StoredAsync());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task A_bad_quantity_is_refused_without_a_write(string quantity)
    {
        var (blue, _, _) = Seed();
        await SignInAsync();

        var response = await AddAsync(blue, quantity);

        Assert.Equal(VariantUrl, Location(response));
        Assert.Equal("Choose a quantity of at least 1.", Notice(await Client.GetStringAsync(VariantUrl), "error"));
        Assert.Empty(await StoredAsync());
    }

    [Fact]
    public async Task An_outside_return_address_is_ignored()
    {
        var (_, _, soldOut) = Seed();
        await SignInAsync();

        var response = await AddAsync(soldOut, returnUrl: "https://evil.example/");

        Assert.Equal("/Cart", Location(response));
    }

    [Fact]
    public async Task Adding_without_the_anti_forgery_token_is_refused()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();

        var response = await Client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["VariantId"] = blue.ToString(),
            ["Quantity"] = "1",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await StoredAsync());
    }

    // ---------- The cart page ----------

    [Fact]
    public async Task An_empty_cart_says_so_and_links_to_the_store()
    {
        await SignInAsync();

        var cart = await Client.GetStringAsync("/Cart");

        Assert.Contains("Your cart is empty", cart);
        Assert.Contains("href=\"/Products\"", cart);
        Assert.Matches("data-cart-count[^>]*>\\s*0\\s*<", cart);
    }

    [Fact]
    public async Task Changing_a_quantity_updates_the_line_and_the_subtotal()
    {
        var (blue, black, _) = Seed();
        await SignInAsync();
        await AddAsync(blue);
        await AddAsync(black);

        var response = await PostFormAsync("/Cart/Update", new() { ["ItemId"] = (await LineIdAsync(blue)).ToString(), ["Quantity"] = "3" }, formPage: "/Cart");

        Assert.Equal("/Cart", Location(response));
        var cart = await PageAsync("/Cart");
        Assert.Contains("100.460.000 VNĐ", cart);
        Assert.Equal([(Email, blue, 3), (Email, black, 1)], await StoredAsync());
    }

    [Fact]
    public async Task Changing_a_quantity_past_the_stock_keeps_the_old_one_and_says_the_stock()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddAsync(blue);

        await PostFormAsync("/Cart/Update", new() { ["ItemId"] = (await LineIdAsync(blue)).ToString(), ["Quantity"] = "9" }, formPage: "/Cart");

        Assert.Equal("Only 3 in stock.", Notice(await Client.GetStringAsync("/Cart"), "error"));
        Assert.Equal([(Email, blue, 1)], await StoredAsync());
    }

    [Fact]
    public async Task Removing_a_line_leaves_the_others()
    {
        var (blue, black, _) = Seed();
        await SignInAsync();
        await AddAsync(blue);
        await AddAsync(black);

        await PostFormAsync("/Cart/Remove", new() { ["ItemId"] = (await LineIdAsync(blue)).ToString() }, formPage: "/Cart");

        Assert.Equal("Removed from your cart.", Notice(await Client.GetStringAsync("/Cart"), "status"));
        Assert.Equal([(Email, black, 1)], await StoredAsync());
    }

    [Fact]
    public async Task Another_users_line_cannot_be_changed_or_removed()
    {
        var (blue, _, _) = Seed();
        await SignInAsync("owner@example.com");
        await AddAsync(blue, "2");
        var ownersLine = (await LineIdAsync(blue)).ToString();
        await PostFormAsync("/Account/Logout", new(), formPage: "/");
        await SignInAsync();

        await PostFormAsync("/Cart/Update", new() { ["ItemId"] = ownersLine, ["Quantity"] = "1" }, formPage: "/Cart");
        var afterUpdate = Notice(await Client.GetStringAsync("/Cart"), "error");
        await PostFormAsync("/Cart/Remove", new() { ["ItemId"] = ownersLine }, formPage: "/Cart");
        var afterRemove = Notice(await Client.GetStringAsync("/Cart"), "error");

        Assert.Equal("That item is no longer in your cart.", afterUpdate);
        Assert.Equal("That item is no longer in your cart.", afterRemove);
        Assert.Equal([("owner@example.com", blue, 2)], await StoredAsync());
    }

    [Fact]
    public async Task A_line_that_left_sale_says_why_and_is_left_out_of_the_subtotal()
    {
        var (blue, black, _) = Seed();
        await SignInAsync();
        await AddAsync(blue);
        await AddAsync(black);
        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants
                .Where(v => v.Id == black).ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, false));

        var cart = await PageAsync("/Cart");

        Assert.Contains("No longer on sale", cart);
        Assert.Matches("data-cart-subtotal[^>]*>\\s*24\\.990\\.000 VNĐ\\s*<", cart);
    }
}

// The database failing under the cart: the visitor gets a message and a way on, not an error page.
public class CartFailureTests : WebFlowTestBase
{
    public CartFailureTests() : base(new AppleStoreWebFactory(configureServices: services =>
    {
        services.RemoveAll<ICartService>();
        services.AddScoped<ICartService, FailingCartService>();
    }))
    {
    }

    [Fact]
    public async Task A_failed_add_says_so_on_the_product_page()
    {
        await CreateUserAsync("shopper@example.com", "Password1");
        await LoginAsync("shopper@example.com", "Password1");

        var response = await PostFormAsync("/Cart/Add", new() { ["VariantId"] = "1", ["Quantity"] = "1", ["ReturnUrl"] = "/Products" }, formPage: "/Cart");

        Assert.Equal("/Products", response.Headers.Location?.OriginalString);
        var page = await Client.GetStringAsync("/Products");
        Assert.Contains("We could not update your cart. Please try again.", WebUtility.HtmlDecode(page));
    }

    private sealed class FailingCartService : ICartService
    {
        private static DbUpdateException Failure() => new("database is locked");

        public Task<CartResult> AddAsync(int userId, int variantId, int quantity, CancellationToken ct = default) => throw Failure();
        public Task<CartResult> SetQuantityAsync(int userId, int itemId, int quantity, CancellationToken ct = default) => throw Failure();
        public Task<CartResult> RemoveAsync(int userId, int itemId, CancellationToken ct = default) => throw Failure();
        public Task<CartView> GetAsync(int userId, CancellationToken ct = default) => Task.FromResult(new CartView([]));
        public Task<int> CountAsync(int userId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
