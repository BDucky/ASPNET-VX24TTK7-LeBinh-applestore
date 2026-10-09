using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Every POST form must carry its own anti-forgery token. The form tag helper
// leaves it out when a form has a hand-written action attribute (found
// 2026-10-08 on the admin pages); the other web tests did not notice because
// they take the token from any form on the page.
public class FormTokenTests : WebFlowTestBase
{
    private static List<string> PostFormsWithoutToken(string html) =>
        Regex.Matches(html, "<form\\b[^>]*method=\"post\"[^>]*>[\\s\\S]*?</form>", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .Where(form => !form.Contains("name=\"__RequestVerificationToken\""))
            .Select(form => form[..Math.Min(form.Length, 160)])
            .ToList();

    [Fact]
    public async Task Every_post_form_on_the_shop_and_admin_pages_carries_its_token()
    {
        var (blue, _, _) = Seed();
        await CreateUserAsync("admin@example.com", "Password1", role: UserRole.Admin);
        await LoginAsync("admin@example.com", "Password1");
        await PostFormAsync("/Cart/Add", new() { ["VariantId"] = blue.ToString(), ["Quantity"] = "1", ["ReturnUrl"] = "/Cart" }, formPage: PhoneVariantUrl);
        var checkout = await Client.GetStringAsync("/Checkout");
        var expected = Regex.Match(checkout, "name=\"ExpectedTotal\" value=\"([^\"]*)\"").Groups[1].Value;
        var placed = await PostFormAsync("/Checkout", new()
        {
            ["FullName"] = "Admin",
            ["Phone"] = "0911111111",
            ["AddressLine"] = "1 Street",
            ["ExpectedTotal"] = expected,
            ["PaymentMethod"] = "Cod",
            ["Intent"] = "place",
        }, formPage: "/Checkout");
        await PostFormAsync("/Cart/Add", new() { ["VariantId"] = blue.ToString(), ["Quantity"] = "1", ["ReturnUrl"] = "/Cart" }, formPage: PhoneVariantUrl);
        int productId, voucherId, promotionId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            productId = await db.Products.Select(p => p.Id).FirstAsync();
            db.Vouchers.Add(new Domain.Entities.Voucher
            {
                Code = "FORMS",
                DiscountValue = 1,
                StartsAt = DateTime.UtcNow,
                EndsAt = DateTime.UtcNow.AddDays(1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            voucherId = await db.Vouchers.Select(v => v.Id).FirstAsync();
            promotionId = SalePricingTests.AddPromotion(db, "Forms", VoucherDiscountType.Percent, 5m, starts: DateTime.UtcNow.AddDays(-1), ends: DateTime.UtcNow.AddDays(1));
        }
        await PostFormAsync("/Compare/Add", new() { ["ProductId"] = productId.ToString(), ["ReturnUrl"] = "/Compare" }, formPage: "/Products/iphone-17");
        var orderPath = placed.Headers.Location!.OriginalString;
        var orderId = orderPath.Split('/')[^1];

        var pages = new[]
        {
            "/", PhoneVariantUrl, "/Cart", "/Checkout", orderPath, "/Orders", "/Track", "/Account", "/Account/Profile", "/Account/ChangePassword",
            "/Account/Addresses/Create", "/Admin", "/Admin/Orders", $"/Admin/Orders/{orderId}", "/Admin/Products", "/Admin/Products/New",
            $"/Admin/Products/{productId}", "/Admin/Vouchers", "/Admin/Vouchers/New", $"/Admin/Vouchers/{voucherId}", "/Admin/Users",
            "/Admin/Reviews", "/Admin/Reports", "/Products/iphone-17", "/Compare", "/Admin/Promotions", "/Admin/Promotions/New", $"/Admin/Promotions/{promotionId}", "/Admin/Prices",
        };
        var missing = new List<string>();
        foreach (var page in pages)
            missing.AddRange(PostFormsWithoutToken(await Client.GetStringAsync(page)).Select(f => $"{page}: {f}"));

        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }
}
