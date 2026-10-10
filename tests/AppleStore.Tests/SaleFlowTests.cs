using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Use case 21 through the real app: staff sell at the counter at
// /Admin/Sales/New (price first, then complete at that total), the sale is
// an order with its invoice, and several invoices print at once
// (BM_INVOICE_01's batch printing).
public class SaleFlowTests : WebFlowTestBase
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

    private static string Field(string html, string name) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

    private int Stock(int variant)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants.Single(v => v.Id == variant).StockQty;
    }

    private async Task<string> FormUrlAsync() => (await Client.GetAsync("/Admin/Sales/New")).Headers.Location!.OriginalString;

    private static Dictionary<string, string> Sale(string key, string intent, string? expected = null, string? email = null, params (int Variant, int Quantity)[] lines)
    {
        var form = new Dictionary<string, string> { ["FormKey"] = key, ["Intent"] = intent, ["Method"] = "Cash", ["CustomerName"] = "", ["CustomerPhone"] = "" };
        if (expected is not null)
            form["ExpectedTotal"] = expected;
        if (email is not null)
            form["CustomerEmail"] = email;
        for (var i = 0; i < lines.Length; i++)
        {
            form[$"Lines[{i}].VariantId"] = lines[i].Variant.ToString();
            form[$"Lines[{i}].Quantity"] = lines[i].Quantity.ToString();
        }
        return form;
    }

    // Price, then complete at the shown total, as a staff member does.
    // The form token comes from a fresh sale form: a used key's address
    // redirects to its sale, which is what Back must do.
    private async Task<HttpResponseMessage> SellAsync(string formUrl, string? email = null, params (int Variant, int Quantity)[] lines)
    {
        var key = formUrl.Split('=')[1];
        var tokenPage = await FormUrlAsync();
        var quote = await PostFormAsync("/Admin/Sales/New", Sale(key, "quote", email: email, lines: lines), formPage: tokenPage);
        var quoted = WebUtility.HtmlDecode(await quote.Content.ReadAsStringAsync());
        var sell = Sale(key, "sell", Field(quoted, "ExpectedTotal"), email, lines);
        sell["QuotedFor"] = Field(quoted, "QuotedFor");
        return await PostFormAsync("/Admin/Sales/New", sell, formPage: tokenPage);
    }

    [Fact]
    public async Task An_employee_prices_and_completes_a_walk_in_sale()
    {
        var (blue, black, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com", "Thu Ngân");
        Assert.Contains("href=\"/Admin/Sales/New\"", await PageAsync("/Admin/Orders"));
        var formUrl = await FormUrlAsync();
        Assert.Matches("^/Admin/Sales/New\\?key=[0-9a-f-]{36}$", formUrl);

        var quote = await PostFormAsync("/Admin/Sales/New", Sale(formUrl.Split('=')[1], "quote", lines: [(blue, 1), (black, 2)]), formPage: formUrl);
        var quotePage = WebUtility.HtmlDecode(await quote.Content.ReadAsStringAsync());
        Assert.Contains("75.970.000 VNĐ", quotePage);
        var sold = await SellAsync(formUrl, lines: [(blue, 1), (black, 2)]);

        Assert.Matches("^/Admin/Orders/\\d+$", sold.Headers.Location?.OriginalString ?? "");
        Assert.Equal((2, 3), (Stock(blue), Stock(black)));
        var details = await PageAsync(sold.Headers.Location!.OriginalString);
        Assert.Equal("Sale completed and paid.", Notice(details, "status"));
        Assert.Contains("In store", details);
        Assert.Contains("Sold by Thu Ngân", details);
        Assert.Contains("Walk-in customer", details);
        var invoice = await PageAsync(sold.Headers.Location!.OriginalString + "/Invoice");
        Assert.Contains("Prices include VAT.", invoice);
        Assert.Contains("75.970.000 VNĐ", invoice);
    }

    [Fact]
    public async Task Back_to_a_completed_sale_form_shows_the_sale_and_sells_nothing_more()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var formUrl = await FormUrlAsync();
        var sold = await SellAsync(formUrl, lines: [(blue, 1)]);

        var back = await Client.GetAsync(formUrl);
        var again = await SellAsync(formUrl, lines: [(blue, 1)]);

        Assert.Equal(sold.Headers.Location!.OriginalString, back.Headers.Location?.OriginalString);
        Assert.Equal(sold.Headers.Location!.OriginalString, again.Headers.Location?.OriginalString);
        Assert.Equal(2, Stock(blue));
    }

    [Fact]
    public async Task Not_enough_stock_is_shown_on_the_form_and_sells_nothing()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");

        var response = await SellAsync(await FormUrlAsync(), lines: [(blue, 9)]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Not enough stock of IP17-256-BLUE.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(3, Stock(blue));
    }

    [Fact]
    public async Task A_price_changed_after_pricing_asks_to_check_the_new_total()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var formUrl = await FormUrlAsync();
        var key = formUrl.Split('=')[1];

        // Priced at one total; the sale arrives with another (as when a
        // promotion starts in between).
        var quoted = WebUtility.HtmlDecode(await (await PostFormAsync("/Admin/Sales/New", Sale(key, "quote", lines: [(blue, 1)]), formPage: formUrl)).Content.ReadAsStringAsync());
        var sell = Sale(key, "sell", "1", lines: [(blue, 1)]);
        sell["QuotedFor"] = Field(quoted, "QuotedFor");
        var response = await PostFormAsync("/Admin/Sales/New", sell, formPage: formUrl);
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("The total changed. Check it and complete the sale again.", page);
        Assert.Equal("24990000", Field(page, "ExpectedTotal"));
        Assert.Equal(3, Stock(blue));
    }

    [Fact]
    public async Task A_sale_under_a_customer_email_shows_in_their_orders()
    {
        var (blue, _, _) = Seed();
        await CreateUserAsync("alice@example.com", Password, "Alice");
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var sold = await SellAsync(await FormUrlAsync(), "alice@example.com", (blue, 1));
        var id = sold.Headers.Location!.OriginalString.Split('/')[^1];

        using var alice = NewClient();
        await LoginAsync(alice, "alice@example.com", Password);
        Assert.Contains($"Order #{id}", WebUtility.HtmlDecode(await alice.GetStringAsync("/Orders")));
    }

    [Fact]
    public async Task Staff_print_several_invoices_at_once()
    {
        var (blue, black, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var first = (await SellAsync(await FormUrlAsync(), lines: [(blue, 1)])).Headers.Location!.OriginalString.Split('/')[^1];
        var second = (await SellAsync(await FormUrlAsync(), lines: [(black, 1)])).Headers.Location!.OriginalString.Split('/')[^1];

        var list = await PageAsync("/Admin/Orders");
        var print = await PageAsync($"/Admin/Orders/Invoices?ids={first}&ids={second}");

        Assert.Contains("action=\"/Admin/Orders/Invoices\"", list);
        Assert.Contains($"name=\"ids\" value=\"{first}\"", list);
        Assert.Contains($"Invoice for order #{first}", print);
        Assert.Contains($"Invoice for order #{second}", print);
        Assert.DoesNotContain("site-nav", print);
    }

    [Fact]
    public async Task Printing_no_invoices_says_so()
    {
        await SignInAsync(UserRole.Employee, "staff@example.com");

        Assert.Contains("Pick at least one order to print.", await PageAsync("/Admin/Orders/Invoices"));
    }

    [Fact]
    public async Task Customers_and_visitors_cannot_sell()
    {
        var anonymous = await Client.GetAsync("/Admin/Sales/New");
        await SignInAsync(UserRole.Customer, "shopper@example.com");
        var customer = await Client.GetAsync("/Admin/Sales/New");

        Assert.StartsWith("/Account/Login", anonymous.Headers.Location!.PathAndQuery);
        Assert.StartsWith("/Account/AccessDenied", customer.Headers.Location!.PathAndQuery);
    }

    // Review 2026-10-10: a refused voucher must not offer "Complete sale".
    [Fact]
    public async Task A_refused_voucher_does_not_offer_to_complete_the_sale()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var formUrl = await FormUrlAsync();
        var form = Sale(formUrl.Split('=')[1], "quote", lines: [(blue, 1)]);
        form["VoucherCode"] = "NOPE";

        var page = WebUtility.HtmlDecode(await (await PostFormAsync("/Admin/Sales/New", form, formPage: formUrl)).Content.ReadAsStringAsync());

        Assert.Contains("That voucher code does not exist.", page);
        Assert.DoesNotContain("value=\"sell\"", page);
        Assert.DoesNotContain("name=\"ExpectedTotal\"", page);
    }

    // Review 2026-10-10: priced for one product, then switched to another at
    // the same price: the sale must be priced again, not sold unseen.
    [Fact]
    public async Task Changing_the_lines_after_pricing_asks_to_price_again()
    {
        var (blue, _, pink) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var formUrl = await FormUrlAsync();
        var key = formUrl.Split('=')[1];
        var quote = await PostFormAsync("/Admin/Sales/New", Sale(key, "quote", lines: [(blue, 1)]), formPage: formUrl);
        var quoted = WebUtility.HtmlDecode(await quote.Content.ReadAsStringAsync());
        var sell = Sale(key, "sell", Field(quoted, "ExpectedTotal"), lines: [(blue, 2)]);
        sell["QuotedFor"] = Field(quoted, "QuotedFor");

        var response = await PostFormAsync("/Admin/Sales/New", sell, formPage: formUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The sale changed after it was priced. Check the new total and complete the sale again.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(3, Stock(blue));
    }
}
