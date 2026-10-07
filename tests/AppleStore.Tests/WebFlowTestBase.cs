using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Shared plumbing for tests that drive the real app over HTTP: one app and
// one cookie-keeping client per test, plus form posting with the page's
// anti-forgery token, sign-in, and users created through UserManager.
public abstract class WebFlowTestBase : IDisposable
{
    protected readonly AppleStoreWebFactory Factory;
    protected readonly HttpClient Client;

    protected WebFlowTestBase() : this(new AppleStoreWebFactory())
    {
    }

    // For a test class that needs its own app setup (a swapped service, say).
    protected WebFlowTestBase(AppleStoreWebFactory factory)
    {
        Factory = factory;
        Client = NewClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // A second browser: its own cookies, same app and database.
    protected HttpClient NewClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    protected Task<HttpResponseMessage> LoginAsync(string email, string password, string? returnUrl = null) =>
        LoginAsync(Client, email, password, returnUrl);

    protected async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/Account/Login" : $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return await PostFormAsync(client, url, new() { ["Email"] = email, ["Password"] = password });
    }

    protected Task<HttpResponseMessage> PostFormAsync(string url, Dictionary<string, string> fields, string? formPage = null) =>
        PostFormAsync(Client, url, fields, formPage);

    // GETs the page holding the form (the post URL itself unless formPage is
    // given), takes its anti-forgery token, and posts the fields with it.
    protected static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> fields, string? formPage = null)
    {
        var page = await client.GetStringAsync(formPage ?? url);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), $"No anti-forgery token on {formPage ?? url}");
        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    protected static string ErrorMessage(string html) =>
        WebUtility.HtmlDecode(Regex.Match(html, "class=\"account-error\"[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value);

    protected async Task<User> CreateUserAsync(string email, string password, string fullName = "Test User", string? phone = null, UserRole role = UserRole.Customer)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            Email = email,
            FullName = fullName,
            Phone = phone,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var result = await users.CreateAsync(user, password);
        Assert.True(result.Succeeded);
        return user;
    }

    protected const string PhoneVariantUrl = "/Products/iphone-17/iphone-17-256gb";

    // One product with three colours of one configuration: blue (3 in stock),
    // black (5) and pink (sold out). Used by the cart and checkout tests.
    protected (int Blue, int Black, int SoldOut) Seed()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppleStore.Infrastructure.Data.AppDbContext>();
        var product = ProductCatalogServiceTests.NewProduct(ProductCatalogServiceTests.NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 20_000_000m);
        var blue = ProductCatalogServiceTests.AddVariant(db, product, "IP17-256-BLUE", 24_990_000m, stock: 3, config: "iPhone 17 256GB", color: "Blue", region: "VN/A");
        var black = ProductCatalogServiceTests.AddVariant(db, product, "IP17-256-BLACK", 25_490_000m, stock: 5, config: "iPhone 17 256GB", color: "Black", region: "VN/A");
        var soldOut = ProductCatalogServiceTests.AddVariant(db, product, "IP17-256-PINK", 24_990_000m, stock: 0, config: "iPhone 17 256GB", color: "Pink", region: "VN/A");
        db.SaveChanges();
        return (blue.Id, black.Id, soldOut.Id);
    }

    protected async Task<User?> FindUserAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync(email);
    }
}
