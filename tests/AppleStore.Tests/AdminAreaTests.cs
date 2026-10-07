using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// The Admin area through the real cookie handler and [Authorize(Roles)]:
// the role comes from the claim AppUserClaimsPrincipalFactory writes at sign-in.
public class AdminAreaTests : WebFlowTestBase
{
    private const string Password = "Password1";

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Dashboard")]
    [InlineData("/Admin/Dashboard/Index")]
    public async Task A_visitor_who_is_not_signed_in_is_sent_to_sign_in(string path)
    {
        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login?ReturnUrl=", response.Headers.Location?.PathAndQuery ?? response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData(UserRole.Customer, "/Admin")]
    [InlineData(UserRole.Customer, "/Admin/Dashboard/Index")]
    [InlineData(UserRole.Employee, "/Admin")]
    public async Task A_signed_in_user_who_is_not_an_admin_gets_access_denied_with_a_way_back(UserRole role, string path)
    {
        await CreateUserAsync("user@example.com", Password, role: role);
        await LoginAsync("user@example.com", Password);

        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.PathAndQuery ?? response.Headers.Location?.OriginalString;
        Assert.StartsWith("/Account/AccessDenied", location);
        var page = await Client.GetStringAsync(location);
        Assert.Contains("You do not have access to this page", page);
        Assert.Contains("href=\"/\"", page);
    }

    [Fact]
    public async Task An_admin_sees_the_dashboard_with_counts_from_the_database()
    {
        await CreateUserAsync("admin@example.com", Password, role: UserRole.Admin);
        await CreateUserAsync("c1@example.com", Password);
        await CreateUserAsync("c2@example.com", Password);
        await CreateUserAsync("staff@example.com", Password, role: UserRole.Employee);
        await LoginAsync("admin@example.com", Password);

        var response = await Client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Dashboard", html);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(await db.Products.CountAsync(), Stat(html, "products"));
        Assert.Equal(await db.Products.CountAsync(p => p.Status), Stat(html, "products-on-sale"));
        Assert.Equal(2, Stat(html, "customers"));
        Assert.Equal(0, Stat(html, "orders"));
        Assert.True(Stat(html, "products") > 0, "the seeded catalog should be counted");
    }

    [Fact]
    public async Task Signing_in_from_the_admin_redirect_lands_back_on_the_admin_page()
    {
        await CreateUserAsync("admin@example.com", Password, role: UserRole.Admin);

        var login = await LoginAsync("admin@example.com", Password, returnUrl: "/Admin");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Admin", login.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task After_signing_out_the_admin_page_asks_to_sign_in_again()
    {
        await CreateUserAsync("admin@example.com", Password, role: UserRole.Admin);
        await LoginAsync("admin@example.com", Password);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/Admin")).StatusCode);

        await PostFormAsync("/Account/Logout", new(), formPage: "/");

        var response = await Client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location?.PathAndQuery ?? response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Only_an_admin_sees_the_admin_link_in_the_nav()
    {
        Assert.DoesNotContain(AdminLink, await Client.GetStringAsync("/"));

        await CreateUserAsync("customer@example.com", Password);
        await LoginAsync("customer@example.com", Password);
        Assert.DoesNotContain(AdminLink, await Client.GetStringAsync("/"));

        await CreateUserAsync("admin@example.com", Password, role: UserRole.Admin);
        using var adminClient = NewClient();
        await LoginAsync(adminClient, "admin@example.com", Password);
        Assert.Contains(AdminLink, await adminClient.GetStringAsync("/"));
    }

    private const string AdminLink = "href=\"/Admin\"";

    private static int Stat(string html, string name)
    {
        var match = Regex.Match(html, $"data-stat=\"{name}\"[^>]*>\\s*([0-9.,]+)\\s*<");
        Assert.True(match.Success, $"No stat '{name}' on the page");
        return int.Parse(match.Groups[1].Value.Replace(".", "").Replace(",", ""));
    }
}
