using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// BM_PRICE_01 through the real app: staff (admins and employees, owner's
// choice 2026-10-09) change prices in batches at /Admin/Prices and read the
// history there; a single price saved on the product page is logged too.
public class PriceAdminFlowTests : WebFlowTestBase
{
    private const string Password = "Password1";

    private async Task SignInAsync(UserRole role, string email, string name = "Test User")
    {
        await CreateUserAsync(email, Password, name, role: role);
        await LoginAsync(email, Password);
    }

    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    private static string Notice(string html, string kind) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"class=\"cart-{kind}\"[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value);

    private int ProductId(string slug)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Single(p => p.Slug == slug).Id;
    }

    private decimal? Price(int variant)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants.Single(v => v.Id == variant).Price;
    }

    private Task<HttpResponseMessage> BatchAsync(string mode, string value, int? product = null) =>
        PostFormAsync("/Admin/Prices/Batch", product is { } id
            ? new() { ["ProductIds"] = id.ToString(), ["Mode"] = mode, ["Value"] = value }
            : new() { ["Mode"] = mode, ["Value"] = value }, formPage: "/Admin/Prices");

    [Fact]
    public async Task An_employee_raises_a_products_prices_and_the_shop_and_history_show_it()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com", "Staff Member");
        Assert.Contains("href=\"/Admin/Prices\"", await PageAsync("/Admin/Orders"));

        var response = await BatchAsync("Percent", "3", ProductId("iphone-17"));

        Assert.Equal("/Admin/Prices", response.Headers.Location?.OriginalString);
        var prices = await PageAsync("/Admin/Prices");
        Assert.Equal("Changed 3 prices.", Notice(prices, "status"));
        Assert.Equal(25_740_000m, Price(blue));
        Assert.Matches("data-choice-price[^>]*>25\\.740\\.000 VNĐ<", await PageAsync(PhoneVariantUrl));
        var row = Regex.Match(prices, "<tr[^>]*data-price-change[\\s\\S]*?IP17-256-BLUE[\\s\\S]*?</tr>").Value;
        Assert.Contains("24.990.000 VNĐ", row);
        Assert.Contains("25.740.000 VNĐ", row);
        Assert.Contains("Staff Member", row);
        Assert.Contains("Batch", row);
    }

    [Fact]
    public async Task A_price_saved_on_the_product_page_is_logged_with_the_admin()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Admin, "admin@example.com", "Admin Person");
        var id = ProductId("iphone-17");
        var form = Regex.Match(await PageAsync($"/Admin/Products/{id}"), $"action=\"/Admin/Products/Variants/{blue}\"[\\s\\S]*?</form>").Value;
        string Field(string name) => Regex.Match(form, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

        await PostFormAsync($"/Admin/Products/Variants/{blue}", new()
        {
            ["Price"] = "23990000",
            ["StockQty"] = "3",
            ["OnSale"] = "true",
            ["SeenStock"] = Field("SeenStock"),
            ["Version"] = Field("Version"),
        }, formPage: $"/Admin/Products/{id}");

        var row = Regex.Match(await PageAsync($"/Admin/Prices?product={id}"), "<tr[^>]*data-price-change[\\s\\S]*?</tr>").Value;
        Assert.Contains("23.990.000 VNĐ", row);
        Assert.Contains("Admin Person", row);
        Assert.Contains("Edited", row);
    }

    [Fact]
    public async Task The_history_can_show_one_product()
    {
        Seed();
        await SignInAsync(UserRole.Admin, "admin@example.com");
        await BatchAsync("Amount", "1000", ProductId("iphone-17"));

        var all = await PageAsync("/Admin/Prices");
        var other = await PageAsync("/Admin/Prices?product=999999");

        Assert.Equal(3, Regex.Matches(all, "data-price-change").Count);
        Assert.Empty(Regex.Matches(other, "data-price-change"));
        Assert.Contains("No price changes yet.", other);
    }

    [Theory]
    [InlineData(null, "Percent", "3", "Pick products or a category.")]
    [InlineData("iphone-17", "Percent", "0", "Enter a change other than 0, and above -100%.")]
    [InlineData("iphone-17", "Amount", "-30000000", "That would bring IP17-256-BLUE to 0 or less. Nothing was changed.")]
    [InlineData("iphone-17", "Amount", "1.000", "Type numbers only, without dots or commas (for example 24990000).")]
    public async Task A_batch_against_the_rules_is_shown_and_changes_nothing(string? slug, string mode, string value, string message)
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");

        var response = await BatchAsync(mode, value, slug is null ? null : ProductId(slug));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(24_990_000m, Price(blue));
    }

    [Fact]
    public async Task Customers_and_visitors_cannot_open_the_prices_page()
    {
        var anonymous = await Client.GetAsync("/Admin/Prices");
        await SignInAsync(UserRole.Customer, "shopper@example.com");
        var customer = await Client.GetAsync("/Admin/Prices");

        Assert.StartsWith("/Account/Login", anonymous.Headers.Location!.PathAndQuery);
        Assert.StartsWith("/Account/AccessDenied", customer.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task A_batch_without_the_form_token_is_rejected()
    {
        Seed();
        await SignInAsync(UserRole.Admin, "admin@example.com");

        var response = await Client.PostAsync("/Admin/Prices/Batch", new FormUrlEncodedContent(new Dictionary<string, string> { ["Mode"] = "Percent", ["Value"] = "5" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
