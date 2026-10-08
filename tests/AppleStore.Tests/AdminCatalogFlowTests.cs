using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// The admin product, voucher and account pages through the real app.
public class AdminCatalogFlowTests : WebFlowTestBase
{
    private const string Password = "Password1";

    private async Task<int> SignInAsync(UserRole role = UserRole.Admin, string email = "admin@example.com")
    {
        var user = await CreateUserAsync(email, Password, role: role);
        await LoginAsync(email, Password);
        return user.Id;
    }

    private async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    private static string Location(HttpResponseMessage r) => r.Headers.Location?.OriginalString ?? "";

    private static string Notice(string html, string kind) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"class=\"cart-{kind}\"[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value);

    private static string Field(string html, string name) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

    private async Task<int> CategoryIdAsync(string slug)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Categories.Where(c => c.Slug == slug).Select(c => c.Id).SingleAsync();
    }

    private Dictionary<string, string> ProductForm(string name, int category, string image = "/img/products/mac-mini-m4.jpg") => new()
    {
        ["Name"] = name,
        ["Description"] = "Small desktop",
        ["CategoryId"] = category.ToString(),
        ["BasePrice"] = "14990000",
        ["OnSale"] = "true",
        ["ImageUrl"] = image,
    };

    // ---------- Access ----------

    [Theory]
    [InlineData("/Admin/Products")]
    [InlineData("/Admin/Vouchers")]
    [InlineData("/Admin/Users")]
    public async Task Catalog_voucher_and_account_pages_are_for_admins_only(string path)
    {
        await SignInAsync(UserRole.Employee, "staff@example.com");
        var denied = await Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);

        using var admin = NewClient();
        await CreateUserAsync("admin@example.com", Password, role: UserRole.Admin);
        await LoginAsync(admin, "admin@example.com", Password);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task The_admin_nav_links_every_admin_page()
    {
        await SignInAsync();

        var nav = await PageAsync("/Admin");

        foreach (var href in new[] { "/Admin/Orders", "/Admin/Products", "/Admin/Vouchers", "/Admin/Users" })
            Assert.Contains($"href=\"{href}\"", nav);
    }

    // ---------- Products ----------

    [Fact]
    public async Task An_admin_adds_a_product_with_a_photo_from_the_library_and_it_shows_in_the_shop()
    {
        Seed();
        await SignInAsync();
        var mac = await CategoryIdAsync("iphone");

        var form = await PageAsync("/Admin/Products/New");
        Assert.Contains("value=\"/img/products/mac-mini-m4.jpg\"", form);
        Assert.Contains("value=\"/img/products/iphone-17.jpg\"", form);
        var response = await PostFormAsync("/Admin/Products/New", ProductForm("Mac mini M5", mac), formPage: "/Admin/Products/New");

        Assert.Matches("^/Admin/Products/\\d+$", Location(response));
        Assert.Equal("Product saved.", Notice(await PageAsync(Location(response)), "status"));
        var shop = await PageAsync("/Products/mac-mini-m5");
        Assert.Contains("Mac mini M5", shop);
        Assert.Contains("/img/products/mac-mini-m4.jpg", shop);
    }

    [Fact]
    public async Task A_photo_from_outside_the_library_is_refused_on_the_form()
    {
        Seed();
        await SignInAsync();

        var response = await PostFormAsync("/Admin/Products/New", ProductForm("Mac mini M5", await CategoryIdAsync("iphone"), "https://evil.example/x.jpg"), formPage: "/Admin/Products/New");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Pick a photo from the list.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task A_stale_product_page_is_told_and_does_not_overwrite()
    {
        Seed();
        await SignInAsync();
        var id = await ProductIdAsync("iphone-17");
        var page = await PageAsync($"/Admin/Products/{id}");
        var fields = ProductForm("First edit", await CategoryIdAsync("iphone"));
        fields["Version"] = Field(page, "Version");
        await PostFormAsync($"/Admin/Products/{id}", new(fields), formPage: $"/Admin/Products/{id}");

        fields["Name"] = "Second edit";
        await PostFormAsync($"/Admin/Products/{id}", fields, formPage: $"/Admin/Products/{id}");

        var after = await PageAsync($"/Admin/Products/{id}");
        Assert.Equal("Someone changed this after you opened it. Check it and save again.", Notice(after, "error"));
        Assert.Equal("First edit", Field(after, "Name"));
    }

    [Fact]
    public async Task Taking_a_product_off_sale_removes_it_from_the_shop()
    {
        Seed();
        await SignInAsync();
        var id = await ProductIdAsync("iphone-17");

        await PostFormAsync($"/Admin/Products/{id}/OffSale", new(), formPage: $"/Admin/Products/{id}");

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/Products/iphone-17")).StatusCode);
        Assert.Equal("Taken off sale. Orders that include it are not changed.", Notice(await PageAsync($"/Admin/Products/{id}"), "status"));
        await PostFormAsync($"/Admin/Products/{id}/OnSale", new(), formPage: $"/Admin/Products/{id}");
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/Products/iphone-17")).StatusCode);
    }

    [Fact]
    public async Task A_variant_edit_after_a_sale_is_refused_with_the_stock_now()
    {
        var (blue, _, _) = Seed();
        await SignInAsync();
        var id = await ProductIdAsync("iphone-17");
        var page = await PageAsync($"/Admin/Products/{id}");
        var row = Regex.Match(page, $"action=\"/Admin/Products/Variants/{blue}\"[\\s\\S]*?</form>").Value;
        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants.Where(v => v.Id == blue)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, 1));

        await PostFormAsync($"/Admin/Products/Variants/{blue}", new()
        {
            ["Price"] = "24990000",
            ["StockQty"] = "50",
            ["OnSale"] = "true",
            ["SeenStock"] = Field(row, "SeenStock"),
            ["Version"] = Field(row, "Version"),
        }, formPage: $"/Admin/Products/{id}");

        Assert.Equal("The stock changed to 1 while you were editing (a sale, or another admin). Check it and save again.",
            Notice(await PageAsync($"/Admin/Products/{id}"), "error"));
    }

    [Fact]
    public async Task An_admin_adds_a_variant()
    {
        Seed();
        await SignInAsync();
        var id = await ProductIdAsync("iphone-17");

        await PostFormAsync($"/Admin/Products/{id}/Variants", new()
        {
            ["Sku"] = "IP17-256-GREEN",
            ["Configuration"] = "iPhone 17 256GB",
            ["Color"] = "Green",
            ["Region"] = "VN/A",
            ["Price"] = "24990000",
            ["StockQty"] = "4",
            ["OnSale"] = "true",
        }, formPage: $"/Admin/Products/{id}");

        Assert.Equal("Variant added.", Notice(await PageAsync($"/Admin/Products/{id}"), "status"));
        Assert.Contains("data-choice-color=\"Green\"", await Client.GetStringAsync(PhoneVariantUrl));
    }

    private async Task<int> ProductIdAsync(string slug)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Where(p => p.Slug == slug).Select(p => p.Id).SingleAsync();
    }

    // ---------- Vouchers ----------

    private static Dictionary<string, string> VoucherForm(string code, string value = "10") => new()
    {
        ["Code"] = code,
        ["Type"] = "Percent",
        ["Value"] = value,
        ["StartsAt"] = "2026-10-01T00:00",
        ["EndsAt"] = "2027-12-31T23:59",
        ["UsageLimit"] = "100",
        ["IsActive"] = "true",
    };

    [Fact]
    public async Task An_admin_creates_edits_and_deletes_a_voucher()
    {
        await SignInAsync();

        var created = await PostFormAsync("/Admin/Vouchers/New", VoucherForm("autumn10"), formPage: "/Admin/Vouchers/New");
        Assert.Matches("^/Admin/Vouchers/\\d+$", Location(created));
        Assert.Contains("AUTUMN10", await PageAsync("/Admin/Vouchers"));

        var page = await PageAsync(Location(created));
        var edit = VoucherForm("AUTUMN20", "20");
        edit["Version"] = Field(page, "Version");
        await PostFormAsync(Location(created), edit, formPage: Location(created));
        Assert.Equal("Voucher saved.", Notice(await PageAsync(Location(created)), "status"));
        Assert.Contains("AUTUMN20", await PageAsync("/Admin/Vouchers"));

        await PostFormAsync(Location(created) + "/Delete", new(), formPage: Location(created));
        Assert.Equal("Voucher deleted. Orders that used it keep their code.", Notice(await PageAsync("/Admin/Vouchers"), "status"));
        Assert.DoesNotContain("AUTUMN20", await PageAsync("/Admin/Vouchers"));
    }

    [Fact]
    public async Task A_voucher_against_the_rules_is_shown_on_the_form()
    {
        await SignInAsync();

        var response = await PostFormAsync("/Admin/Vouchers/New", VoucherForm("BIG", "150"), formPage: "/Admin/Vouchers/New");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("A percent discount is at most 100.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    // ---------- Accounts ----------

    [Fact]
    public async Task Making_an_account_staff_takes_effect_at_once_and_so_does_taking_it_back()
    {
        var staff = await CreateUserAsync("person@example.com", Password);
        using var person = NewClient();
        await LoginAsync(person, "person@example.com", Password);
        Assert.Equal(HttpStatusCode.Redirect, (await person.GetAsync("/Admin/Orders")).StatusCode);
        await SignInAsync();

        await PostFormAsync($"/Admin/Users/{staff.Id}/Role", new() { ["Role"] = "Employee" }, formPage: "/Admin/Users");
        Assert.Equal("Role changed. Their open sessions were signed out.", Notice(await PageAsync("/Admin/Users"), "status"));

        var stale = await person.GetAsync("/Account");
        Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
        Assert.Contains("/Account/Login", stale.Headers.Location!.ToString());
        await LoginAsync(person, "person@example.com", Password);
        Assert.Equal(HttpStatusCode.OK, (await person.GetAsync("/Admin/Orders")).StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_change_their_own_role()
    {
        var me = await SignInAsync();

        await PostFormAsync($"/Admin/Users/{me}/Role", new() { ["Role"] = "Customer" }, formPage: "/Admin/Users");

        Assert.Equal("You cannot change your own role.", Notice(await PageAsync("/Admin/Users"), "error"));
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/Admin/Users")).StatusCode);
    }

    [Fact]
    public async Task The_account_list_finds_people()
    {
        await CreateUserAsync("alice@example.com", Password, fullName: "Alice Nguyen");
        await SignInAsync();

        var html = await PageAsync("/Admin/Users?search=nguyen");

        Assert.Contains("alice@example.com", html);
        Assert.DoesNotContain("admin@example.com", html);
    }

    [Fact]
    public async Task Admin_posts_need_the_anti_forgery_token()
    {
        await SignInAsync();

        var response = await Client.PostAsync("/Admin/Vouchers/New", new FormUrlEncodedContent(VoucherForm("NOTOKEN")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
