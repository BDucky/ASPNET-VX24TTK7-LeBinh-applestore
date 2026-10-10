using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Use case 20 through the real app: staff (admins and employees) receive
// goods at /Admin/Stock, see and print the receipt, and check stock levels.
public class StockFlowTests : WebFlowTestBase
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

    private int Stock(int variant)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants.Single(v => v.Id == variant).StockQty;
    }

    private int Receipts()
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().StockReceipts.Count();
    }

    // Lines are posted as Lines[i].VariantId / Quantity / UnitCost; an
    // empty row (no variant, no quantity) is skipped.
    private static Dictionary<string, string> Form(string formKey, params (string Variant, string Quantity, string Cost)[] lines)
    {
        var form = new Dictionary<string, string> { ["Supplier"] = "Công ty FPT Trading", ["Note"] = "Lô 1", ["FormKey"] = formKey };
        for (var i = 0; i < lines.Length; i++)
        {
            form[$"Lines[{i}].VariantId"] = lines[i].Variant;
            form[$"Lines[{i}].Quantity"] = lines[i].Quantity;
            form[$"Lines[{i}].UnitCost"] = lines[i].Cost;
        }
        return form;
    }

    // The form lives at /Admin/Stock/New?key=..., so Back returns to the same key.
    private async Task<string> FormKeyAsync()
    {
        var fresh = await Client.GetAsync("/Admin/Stock/New");
        var url = fresh.Headers.Location!.OriginalString;
        return Regex.Match(await PageAsync(url), "name=\"FormKey\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    [Fact]
    public async Task An_employee_receives_goods_and_sees_the_receipt()
    {
        var (blue, black, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com", "Kho Nhân Viên");
        Assert.Contains("href=\"/Admin/Stock\"", await PageAsync("/Admin/Orders"));

        var response = await PostFormAsync("/Admin/Stock/New",
            Form(await FormKeyAsync(), (blue.ToString(), "10", "21000000"), ("", "", ""), (black.ToString(), "2", "21500000")), formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");

        Assert.Matches("^/Admin/Stock/\\d+$", response.Headers.Location?.OriginalString ?? "");
        Assert.Equal((13, 7), (Stock(blue), Stock(black)));
        var receipt = await PageAsync(response.Headers.Location!.OriginalString);
        Assert.Equal("Receipt saved. The stock is updated.", Notice(receipt, "status"));
        Assert.Contains("Công ty FPT Trading", receipt);
        Assert.Contains("Kho Nhân Viên", receipt);
        Assert.Matches("IP17-256-BLUE[\\s\\S]*?>3<[\\s\\S]*?>13<", receipt);
        Assert.Contains("253.000.000 VNĐ", receipt);
    }

    [Fact]
    public async Task Pressing_save_twice_saves_one_receipt()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var key = await FormKeyAsync();

        var first = await PostFormAsync("/Admin/Stock/New", Form(key, (blue.ToString(), "10", "1")), formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");
        var second = await PostFormAsync("/Admin/Stock/New", Form(key, (blue.ToString(), "10", "1")), formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");

        Assert.Equal(first.Headers.Location?.OriginalString, second.Headers.Location?.OriginalString);
        Assert.Equal((13, 1), (Stock(blue), Receipts()));
        Assert.Equal("This form was already saved as a receipt, so nothing was added. To receive other goods, start a new receipt.", Notice(await PageAsync(second.Headers.Location!.OriginalString), "status"));
    }

    // Found by the live check 2026-10-10: after saving, the browser's Back
    // fetched the form again with a new key, and saving again received the
    // goods twice. The key is in the address now, so Back comes to the same
    // key, and a used key leads to the saved receipt instead of the form.
    [Fact]
    public async Task Back_to_a_saved_form_shows_the_receipt_and_cannot_save_it_again()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var fresh = await Client.GetAsync("/Admin/Stock/New");
        var formUrl = fresh.Headers.Location!.OriginalString;
        Assert.Matches("^/Admin/Stock/New\\?key=[0-9a-f-]{36}$", formUrl);
        var key = Regex.Match(await PageAsync(formUrl), "name=\"FormKey\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal(formUrl.Split('=')[1], key);
        var saved = await PostFormAsync("/Admin/Stock/New", Form(key, (blue.ToString(), "10", "1")), formPage: formUrl);
        await Client.GetStringAsync(saved.Headers.Location!.OriginalString);

        var back = await Client.GetAsync(formUrl);

        Assert.Equal(saved.Headers.Location!.OriginalString, back.Headers.Location?.OriginalString);
        Assert.Equal("This form was already saved as a receipt, so nothing was added. To receive other goods, start a new receipt.", Notice(await PageAsync(back.Headers.Location!.OriginalString), "status"));
        Assert.Equal((13, 1), (Stock(blue), Receipts()));
    }

    [Theory]
    [InlineData("dup", "IP17-256-BLUE is on the receipt twice. Put it on one line.")]
    [InlineData("none", "Add at least one line.")]
    [InlineData("supplier", "Enter the supplier.")]
    [InlineData("zero", "A quantity is at least 1.")]
    [InlineData("dots", "Type numbers only, without dots or commas (for example 24990000).")]
    [InlineData("letters", "Type numbers only, without dots or commas (for example 24990000).")]
    public async Task A_receipt_against_the_rules_is_shown_on_the_form_and_changes_nothing(string which, string message)
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var key = await FormKeyAsync();
        var b = blue.ToString();
        var form = which switch
        {
            "dup" => Form(key, (b, "1", "1"), (b, "2", "1")),
            "none" => Form(key, ("", "", "")),
            "supplier" => Form(key, (b, "1", "1")),
            "zero" => Form(key, (b, "0", "1")),
            "dots" => Form(key, (b, "1", "1.000")),
            _ => Form(key, (b, "abc", "1")),
        };
        if (which == "supplier")
            form["Supplier"] = " ";

        var response = await PostFormAsync("/Admin/Stock/New", form, formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal((3, 0), (Stock(blue), Receipts()));
    }

    [Fact]
    public async Task The_receipt_prints_without_the_shop_around_it()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Admin, "admin@example.com");
        var saved = await PostFormAsync("/Admin/Stock/New", Form(await FormKeyAsync(), (blue.ToString(), "4", "100")), formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");

        var print = await PageAsync(saved.Headers.Location!.OriginalString + "/Print");

        Assert.DoesNotContain("site-nav", print);
        Assert.Contains("Stock receipt #", print);
        Assert.Contains("Công ty FPT Trading", print);
    }

    [Fact]
    public async Task Stock_levels_list_every_variant_lowest_first_and_search()
    {
        Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");

        var all = await PageAsync("/Admin/Stock/Levels");
        var search = await PageAsync("/Admin/Stock/Levels?q=black");

        var skus = Regex.Matches(all, "data-stock-sku=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["IP17-256-PINK", "IP17-256-BLUE", "IP17-256-BLACK"], skus);
        Assert.Equal(["IP17-256-BLACK"], Regex.Matches(search, "data-stock-sku=\"([^\"]+)\"").Select(m => m.Groups[1].Value));
    }

    [Fact]
    public async Task Customers_and_visitors_cannot_open_the_stock_pages()
    {
        var anonymous = await Client.GetAsync("/Admin/Stock");
        await SignInAsync(UserRole.Customer, "shopper@example.com");
        var customer = await Client.GetAsync("/Admin/Stock/Levels");

        Assert.StartsWith("/Account/Login", anonymous.Headers.Location!.PathAndQuery);
        Assert.StartsWith("/Account/AccessDenied", customer.Headers.Location!.PathAndQuery);
    }

    // Review 2026-10-10: a duplicated tab reuses the key; its different goods
    // must not look received.
    [Fact]
    public async Task A_second_tab_with_the_same_key_is_told_to_start_a_new_receipt()
    {
        var (blue, black, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var key = await FormKeyAsync();
        await PostFormAsync("/Admin/Stock/New", Form(key, (blue.ToString(), "10", "1")), formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");

        var second = await PostFormAsync("/Admin/Stock/New", Form(key, (black.ToString(), "4", "1")), formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");
        var page = await PageAsync(second.Headers.Location!.OriginalString);

        Assert.Contains("start a new receipt", Notice(page, "status"));
        Assert.Contains("href=\"/Admin/Stock/New\"", page);
        Assert.Equal((5, 1), (Stock(black), Receipts()));
    }

    [Fact]
    public async Task An_empty_form_key_is_never_used()
    {
        var (blue, _, _) = Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");

        var get = await Client.GetAsync("/Admin/Stock/New?key=00000000-0000-0000-0000-000000000000");
        var post = await PostFormAsync("/Admin/Stock/New", Form("00000000-0000-0000-0000-000000000000", (blue.ToString(), "1", "1")), formPage: "/Admin/Stock/New?key=00000000-0000-0000-0000-000000000001");

        Assert.Matches("^/Admin/Stock/New\\?key=(?!00000000-0000-0000-0000-000000000000)", get.Headers.Location!.OriginalString);
        Assert.StartsWith("/Admin/Stock/New", post.Headers.Location!.OriginalString);
        Assert.Equal((3, 0), (Stock(blue), Receipts()));
    }
}
