using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppleStore.Tests;

// Checkout through the real app: [Authorize], anti-forgery, model
// validation, the real CartService and CheckoutService.
public class CheckoutFlowTests : WebFlowTestBase
{
    private const string Password = "Password1";
    private const string Email = "buyer@example.com";

    private async Task<int> SignInAsync(string email = Email)
    {
        var user = await CreateUserAsync(email, Password, fullName: "Buyer Name", phone: "0911111111");
        await LoginAsync(email, Password);
        return user.Id;
    }

    private Task AddToCartAsync(int variantId, int quantity = 1) =>
        PostFormAsync("/Cart/Add", new() { ["VariantId"] = variantId.ToString(), ["Quantity"] = quantity.ToString(), ["ReturnUrl"] = PhoneVariantUrl }, formPage: PhoneVariantUrl);

    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    private static string Location(HttpResponseMessage response) => response.Headers.Location?.OriginalString ?? "";

    private static string Notice(string html, string kind) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"class=\"cart-{kind}\"[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value);

    private static string Field(string html, string name) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

    private static string Total(string html) =>
        Regex.Match(WebUtility.HtmlDecode(html), "data-checkout-total[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value;

    // A user without a saved address has to type one before placing.
    private static Dictionary<string, string> WithAddress => new() { ["AddressLine"] = "5 Test Street" };

    private Dictionary<string, string> Form(string html, string intent, Dictionary<string, string>? overrides = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["FullName"] = Field(html, "FullName"),
            ["Phone"] = Field(html, "Phone"),
            ["AddressLine"] = Field(html, "AddressLine"),
            ["Ward"] = Field(html, "Ward"),
            ["District"] = Field(html, "District"),
            ["City"] = Field(html, "City"),
            ["Note"] = "",
            ["VoucherCode"] = Field(html, "VoucherCode"),
            ["ExpectedTotal"] = Field(html, "ExpectedTotal"),
            ["Intent"] = intent,
        };
        foreach (var (k, v) in overrides ?? new Dictionary<string, string>())
            fields[k] = v;
        return fields;
    }

    private async Task<int> AddVoucherAsync(string code, VoucherDiscountType type, decimal value, DateTime? ends = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var voucher = new Voucher
        {
            Code = code,
            DiscountType = type,
            DiscountValue = value,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            EndsAt = ends ?? DateTime.UtcNow.AddDays(30),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
        return voucher.Id;
    }

    private async Task<int> OrderCountAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.CountAsync();
    }

    private async Task SaveDefaultAddressAsync(int userId)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProfileService>().AddAddressAsync(userId,
            new AddressInput("Home", "Receiver Home", "0922222222", "12 Nguyen Hue", "Ben Nghe", "District 1", "Ho Chi Minh City", IsDefault: true));
    }

    // ---------- Getting to checkout ----------

    [Fact]
    public async Task A_visitor_is_sent_to_sign_in()
    {
        var response = await Client.GetAsync("/Checkout");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login?ReturnUrl=%2FCheckout", response.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task An_empty_cart_goes_back_to_the_cart_and_says_so()
    {
        await SignInAsync();

        var response = await Client.GetAsync("/Checkout");

        Assert.Equal("/Cart", Location(response));
        Assert.Equal("Your cart is empty.", Notice(await PageAsync("/Cart"), "error"));
    }

    [Fact]
    public async Task A_cart_with_a_line_that_cannot_be_bought_goes_back_to_the_cart()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);
        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants
                .Where(v => v.Id == blue).ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, false));

        var cart = await PageAsync("/Cart");
        var response = await Client.GetAsync("/Checkout");

        Assert.DoesNotContain("href=\"/Checkout\"", cart);
        Assert.Equal("/Cart", Location(response));
        Assert.Equal("Remove or change the lines marked in red, then check out.", Notice(await PageAsync("/Cart"), "error"));
    }

    [Fact]
    public async Task The_cart_links_to_checkout_which_shows_the_order_and_the_default_address()
    {
        var (blue, _, _) = Seed();
        var userId = await SignInAsync();
        await SaveDefaultAddressAsync(userId);
        await AddToCartAsync(blue, 2);

        Assert.Contains("href=\"/Checkout\"", await PageAsync("/Cart"));
        var page = await PageAsync("/Checkout");

        Assert.Equal(("Receiver Home", "0922222222", "12 Nguyen Hue", "Ho Chi Minh City"),
            (Field(page, "FullName"), Field(page, "Phone"), Field(page, "AddressLine"), Field(page, "City")));
        Assert.Contains("iPhone 17 256GB", page);
        Assert.Equal("49.980.000 VNĐ", Total(page));
        Assert.Equal("49980000", Field(page, "ExpectedTotal").Split('.', ',')[0]);
        Assert.Contains("Free", page);
    }

    [Fact]
    public async Task Without_a_saved_address_the_form_starts_from_the_profile()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);

        var page = await PageAsync("/Checkout");

        Assert.Equal(("Buyer Name", "0911111111", ""), (Field(page, "FullName"), Field(page, "Phone"), Field(page, "AddressLine")));
    }

    // ---------- Voucher ----------

    [Fact]
    public async Task Applying_a_voucher_shows_the_discount_and_keeps_what_was_typed()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);
        await AddVoucherAsync("WELCOME10", VoucherDiscountType.Percent, 10m);
        var page = await PageAsync("/Checkout");

        var response = await PostFormAsync("/Checkout", Form(page, "apply", new() { ["VoucherCode"] = "welcome10", ["AddressLine"] = "99 Typed Street" }), formPage: "/Checkout");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var applied = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Voucher WELCOME10 applied.", applied);
        Assert.Contains("-2.499.000 VNĐ", applied);
        Assert.Equal("22.491.000 VNĐ", Total(applied));
        Assert.Equal("99 Typed Street", Field(applied, "AddressLine"));
        Assert.Equal(0, await OrderCountAsync());
    }

    [Theory]
    [InlineData("NOPE", "There is no voucher with that code.")]
    [InlineData("OLD", "This voucher has expired.")]
    public async Task A_voucher_that_does_not_apply_says_why_and_leaves_the_total(string code, string message)
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);
        await AddVoucherAsync("OLD", VoucherDiscountType.Percent, 10m, ends: DateTime.UtcNow.AddDays(-1));
        var page = await PageAsync("/Checkout");

        var response = await PostFormAsync("/Checkout", Form(page, "apply", new() { ["VoucherCode"] = code }), formPage: "/Checkout");

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(message, html);
        Assert.Equal("24.990.000 VNĐ", Total(html));
    }

    // ---------- Placing ----------

    [Fact]
    public async Task Placing_shows_the_order_and_empties_the_cart()
    {
        var (blue, black, _) = Seed();
        var userId = await SignInAsync();
        await SaveDefaultAddressAsync(userId);
        await AddToCartAsync(blue);
        await AddToCartAsync(black);
        await AddVoucherAsync("WELCOME10", VoucherDiscountType.Percent, 10m);
        var page = await PageAsync("/Checkout");
        var applied = WebUtility.HtmlDecode(await (await PostFormAsync("/Checkout", Form(page, "apply", new() { ["VoucherCode"] = "WELCOME10" }), formPage: "/Checkout")).Content.ReadAsStringAsync());

        var response = await PostFormAsync("/Checkout", Form(applied, "place", new() { ["Note"] = "Ring twice" }), formPage: "/Checkout");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Matches("^/Orders/\\d+$", Location(response));
        var order = await PageAsync(Location(response));
        Assert.Equal("Thank you. Your order was placed.", Notice(order, "status"));
        Assert.Contains("iPhone 17 256GB", order);
        Assert.Contains("Ring twice", order);
        Assert.Contains("Cash on delivery", order);
        Assert.Contains("45.432.000 VNĐ", order);
        Assert.Matches("data-cart-count[^>]*>\\s*0\\s*<", order);
        Assert.Equal(1, await OrderCountAsync());
    }

    [Fact]
    public async Task Missing_delivery_details_are_shown_on_the_form_and_nothing_is_placed()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);
        var page = await PageAsync("/Checkout");

        var response = await PostFormAsync("/Checkout", Form(page, "place", new() { ["AddressLine"] = "", ["Phone"] = "not a phone" }), formPage: "/Checkout");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("The Street address field is required.", html);
        Assert.Contains("The Receiver phone field is not a valid phone number.", html);
        Assert.Equal(0, await OrderCountAsync());
    }

    [Fact]
    public async Task A_total_that_changed_since_the_page_was_opened_is_shown_again_and_nothing_is_placed()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);
        var page = await PageAsync("/Checkout");
        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants
                .Where(v => v.Id == blue).ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, 25_990_000m));

        var response = await PostFormAsync("/Checkout", Form(page, "place", WithAddress), formPage: "/Checkout");

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("The total changed since you opened this page. Check it and place the order again.", html);
        Assert.Equal("25.990.000 VNĐ", Total(html));
        Assert.Equal(0, await OrderCountAsync());

        // Having seen the new total, placing again goes through.
        var again = await PostFormAsync("/Checkout", Form(html, "place", WithAddress), formPage: "/Checkout");
        Assert.Matches("^/Orders/\\d+$", Location(again));
        Assert.Equal(1, await OrderCountAsync());
    }

    [Fact]
    public async Task Placing_twice_makes_one_order_and_the_second_goes_to_the_cart()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);
        var page = await PageAsync("/Checkout");

        await PostFormAsync("/Checkout", Form(page, "place", WithAddress), formPage: "/Checkout");
        var second = await PostFormAsync("/Checkout", Form(page, "place", WithAddress), formPage: "/Cart");

        Assert.Equal("/Cart", Location(second));
        Assert.Equal(1, await OrderCountAsync());
    }

    [Fact]
    public async Task Placing_without_the_anti_forgery_token_is_refused()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        await AddToCartAsync(blue);
        var page = await PageAsync("/Checkout");

        var response = await Client.PostAsync("/Checkout", new FormUrlEncodedContent(Form(page, "place")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await OrderCountAsync());
    }

    [Fact]
    public async Task Another_users_order_is_not_found()
    {
        var (blue, _, _) = Seed();
        await SignInAsync("first@example.com");
        await AddToCartAsync(blue);
        var placed = await PostFormAsync("/Checkout", Form(await PageAsync("/Checkout"), "place", WithAddress), formPage: "/Checkout");
        await PostFormAsync("/Account/Logout", new(), formPage: "/");
        await SignInAsync();

        var response = await Client.GetAsync(Location(placed));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

// The database failing while placing: a message and the way back, nothing half-done.
public class CheckoutFailureTests : WebFlowTestBase
{
    public CheckoutFailureTests() : base(new AppleStoreWebFactory(configureServices: services =>
    {
        services.RemoveAll<ICheckoutService>();
        services.AddScoped<ICheckoutService, FailingCheckoutService>();
    }))
    {
    }

    [Fact]
    public async Task A_failed_order_says_nothing_was_placed()
    {
        await CreateUserAsync("buyer@example.com", "Password1");
        await LoginAsync("buyer@example.com", "Password1");
        var page = WebUtility.HtmlDecode(await Client.GetStringAsync("/Checkout"));
        var token = Regex.Match(page, "name=\"ExpectedTotal\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

        var response = await PostFormAsync("/Checkout", new()
        {
            ["FullName"] = "Buyer",
            ["Phone"] = "0911111111",
            ["AddressLine"] = "1 Street",
            ["ExpectedTotal"] = token,
            ["Intent"] = "place",
        }, formPage: "/Checkout");

        Assert.Equal("/Checkout", response.Headers.Location?.OriginalString);
        var after = WebUtility.HtmlDecode(await Client.GetStringAsync("/Checkout"));
        Assert.Contains("We could not place your order, and nothing was charged. Please try again.", after);
    }

    private sealed class FailingCheckoutService : ICheckoutService
    {
        private static readonly CartView Cart = new([new CartLine(1, 1, 1, "Thing", "thing", "Thing", "thing", null, null, null, 100m, 1, 5, CartLineProblem.None)]);

        public Task<CheckoutQuote> QuoteAsync(int userId, string? voucherCode, CancellationToken ct = default) =>
            Task.FromResult(new CheckoutQuote(Cart, 100m, 0m, 0m, 100m, null, VoucherProblem.None, CheckoutProblem.None));

        public Task<PlaceOrderResult> PlaceOrderAsync(int userId, DeliveryInput delivery, string? voucherCode, decimal expectedTotal, CancellationToken ct = default) =>
            throw new DbUpdateException("database is locked");

        public Task<OrderSummary?> GetOrderAsync(int userId, int orderId, CancellationToken ct = default) => Task.FromResult<OrderSummary?>(null);
    }
}
