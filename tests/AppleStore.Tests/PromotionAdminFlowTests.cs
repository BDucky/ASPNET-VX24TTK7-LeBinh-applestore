using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Use case 28 through the real app: staff (admins and employees, per the
// report) run promotions at /Admin/Promotions, and the shop shows and
// charges the lower price at once. Code vouchers stay admin-only.
public class PromotionAdminFlowTests : WebFlowTestBase
{
    private const string Password = "Password1";

    private async Task SignInAsync(UserRole role, string email)
    {
        await CreateUserAsync(email, Password, role: role);
        await LoginAsync(email, Password);
    }

    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    private static string Location(HttpResponseMessage r) => r.Headers.Location?.OriginalString ?? "";

    private static string Notice(string html, string kind) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"class=\"cart-{kind}\"[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value);

    private int ProductId(string slug)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Single(p => p.Slug == slug).Id;
    }

    private static Dictionary<string, string> PromotionForm(string name, string value = "10", int? product = null)
    {
        var form = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Type"] = "Percent",
            ["Value"] = value,
            ["StartsAt"] = DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm"),
            ["EndsAt"] = DateTime.Now.AddDays(7).ToString("yyyy-MM-ddTHH:mm"),
            ["IsActive"] = "true",
        };
        if (product is { } id)
            form["ProductIds"] = id.ToString();
        return form;
    }

    private int AddCodeVoucher()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Vouchers.Add(new Domain.Entities.Voucher
        {
            Code = "KEEP10",
            DiscountValue = 10,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            EndsAt = DateTime.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return db.Vouchers.Single(v => v.Code == "KEEP10").Id;
    }

    [Fact]
    public async Task An_employee_starts_a_promotion_and_the_shop_shows_and_charges_it()
    {
        Seed();
        await SignInAsync(UserRole.Employee, "staff@example.com");
        Assert.Contains("href=\"/Admin/Promotions\"", await PageAsync("/Admin/Orders"));

        var created = await PostFormAsync("/Admin/Promotions/New", PromotionForm("Phone week", product: ProductId("iphone-17")), formPage: "/Admin/Promotions/New");

        Assert.Matches("^/Admin/Promotions/\\d+$", Location(created));
        Assert.Equal("Promotion saved.", Notice(await PageAsync(Location(created)), "status"));
        Assert.Contains("Phone week", await PageAsync("/Admin/Promotions"));
        var variant = await PageAsync(PhoneVariantUrl);
        Assert.Matches("data-choice-price[^>]*>22\\.491\\.000 VNĐ<", variant);
        Assert.Matches("data-choice-was[^>]*>24\\.990\\.000 VNĐ<", variant);
        Assert.Contains("Phone week", variant);
        Assert.Matches("class=\"price-was\"[^>]*>24\\.990\\.000 VNĐ<", await PageAsync("/Products/iphone-17"));
    }

    [Fact]
    public async Task The_cart_charges_the_promotion_price_and_shows_the_old_one()
    {
        var (blue, _, _) = Seed();
        using (var scope = Factory.Services.CreateScope())
            SalePricingTests.AddPromotion(scope.ServiceProvider.GetRequiredService<AppDbContext>(), "Phone week", VoucherDiscountType.Percent, 10m,
                starts: DateTime.UtcNow.AddDays(-1), ends: DateTime.UtcNow.AddDays(7));
        await SignInAsync(UserRole.Customer, "shopper@example.com");

        await PostFormAsync("/Cart/Add", new() { ["VariantId"] = blue.ToString(), ["Quantity"] = "1", ["ReturnUrl"] = "/Cart" }, formPage: PhoneVariantUrl);
        var cart = await PageAsync("/Cart");

        Assert.Matches("data-cart-subtotal[^>]*>\\s*22\\.491\\.000 VNĐ\\s*<", cart);
        Assert.Matches("class=\"price-was\"[^>]*>24\\.990\\.000 VNĐ<", cart);
    }

    [Fact]
    public async Task A_promotion_form_has_a_name_and_no_code_minimum_or_limit()
    {
        await SignInAsync(UserRole.Employee, "staff@example.com");

        var form = await PageAsync("/Admin/Promotions/New");

        Assert.Contains("name=\"Name\"", form);
        Assert.DoesNotContain("name=\"Code\"", form);
        Assert.DoesNotContain("name=\"MinOrderAmount\"", form);
        Assert.DoesNotContain("name=\"UsageLimit\"", form);
    }

    // A hand-made post with voucher fields still saves a promotion: no code,
    // no minimum, no limit.
    [Fact]
    public async Task Posted_voucher_fields_are_not_saved_on_a_promotion()
    {
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var form = PromotionForm("Sneaky");
        form["Code"] = "SNEAKY";
        form["MinOrderAmount"] = "1";
        form["UsageLimit"] = "1";

        await PostFormAsync("/Admin/Promotions/New", form, formPage: "/Admin/Promotions/New");

        using var scope = Factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Vouchers.SingleAsync();
        Assert.Equal((VoucherKind.Automatic, (string?)null, (decimal?)null, (int?)null), (saved.Kind, saved.Code, saved.MinOrderAmount, saved.UsageLimit));
    }

    [Theory]
    [InlineData("", "10", "Enter a name for the promotion.")]
    [InlineData("Free phones", "100", "A promotion takes off less than 100%, so nothing is given away.")]
    public async Task A_promotion_against_the_rules_is_shown_on_the_form(string name, string value, string message)
    {
        await SignInAsync(UserRole.Employee, "staff@example.com");

        var response = await PostFormAsync("/Admin/Promotions/New", PromotionForm(name, value), formPage: "/Admin/Promotions/New");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task An_employee_cannot_reach_a_code_voucher_through_the_promotions_page()
    {
        var voucher = AddCodeVoucher();
        await SignInAsync(UserRole.Employee, "staff@example.com");

        var open = await Client.GetAsync($"/Admin/Promotions/{voucher}");
        var delete = await PostFormAsync($"/Admin/Promotions/{voucher}/Delete", new(), formPage: "/Admin/Promotions");
        var vouchers = await Client.GetAsync("/Admin/Vouchers");

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.Redirect), (open.StatusCode, delete.StatusCode, vouchers.StatusCode));
        using var scope = Factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Vouchers.AnyAsync(v => v.Id == voucher));
    }

    [Fact]
    public async Task Customers_and_visitors_cannot_open_the_promotions_page()
    {
        var anonymous = await Client.GetAsync("/Admin/Promotions");
        await SignInAsync(UserRole.Customer, "shopper@example.com");
        var customer = await Client.GetAsync("/Admin/Promotions");

        Assert.StartsWith("/Account/Login", Location(anonymous));
        Assert.StartsWith("/Account/AccessDenied", Location(customer));
    }

    [Fact]
    public async Task Deleting_a_promotion_puts_the_old_price_back()
    {
        Seed();
        await SignInAsync(UserRole.Admin, "admin@example.com");
        var created = await PostFormAsync("/Admin/Promotions/New", PromotionForm("Short"), formPage: "/Admin/Promotions/New");

        await PostFormAsync(Location(created) + "/Delete", new(), formPage: Location(created));

        Assert.Equal("Promotion deleted. Orders placed during it keep the prices they were charged.", Notice(await PageAsync("/Admin/Promotions"), "status"));
        Assert.Matches("data-choice-price[^>]*>24\\.990\\.000 VNĐ<", await PageAsync(PhoneVariantUrl));
    }
}
