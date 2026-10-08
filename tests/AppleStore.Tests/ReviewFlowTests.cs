using System.Net;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Reviews on the product page and for staff, through the real app.
public class ReviewFlowTests : PaymentFlowBase
{
    private const string ProductPage = "/Products/iphone-17";

    private async Task<int> DeliveredOrderAsync()
    {
        await PlaceAsync(await ReadyToCheckOutAsync(), "Cod");
        var order = await OnlyOrderIdAsync();
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == order)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Completed).SetProperty(o => o.PaymentStatus, OrderPaymentStatus.Paid));
        return order;
    }

    private async Task<int> ProductIdAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Where(p => p.Slug == "iphone-17").Select(p => p.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> ReviewAsync(string rating, string content) =>
        await PostFormAsync($"/Reviews/{await ProductIdAsync()}", new() { ["Rating"] = rating, ["Content"] = content, ["ReturnUrl"] = ProductPage }, formPage: ProductPage);

    private async Task SignInAsAsync(string email, UserRole role)
    {
        if ((await Client.GetStringAsync("/")).Contains("site-nav-account-out"))
            await PostFormAsync("/Account/Logout", new(), formPage: "/");
        await CreateUserAsync(email, Password, role: role);
        await LoginAsync(email, Password);
    }

    [Fact]
    public async Task A_visitor_reads_reviews_but_gets_no_form()
    {
        Seed();

        var html = await PageAsync(ProductPage);

        Assert.Contains("No reviews yet.", html);
        Assert.DoesNotContain("name=\"Rating\"", html);
    }

    [Fact]
    public async Task A_customer_who_has_not_received_it_is_told_when_they_can_review()
    {
        await PlaceAsync(await ReadyToCheckOutAsync(), "Cod");

        var html = await PageAsync(ProductPage);

        Assert.Contains("You can review this after your order with it is delivered.", html);
        Assert.DoesNotContain("name=\"Rating\"", html);
        await ReviewAsync("5", "sneaky");
        Assert.Equal("You can review this after your order with it is delivered.", Notice(await PageAsync(ProductPage), "error"));
    }

    [Fact]
    public async Task A_customer_with_a_delivered_order_reviews_and_sees_it_with_the_average()
    {
        await DeliveredOrderAsync();
        Assert.Contains("name=\"Rating\"", await PageAsync(ProductPage));

        var response = await ReviewAsync("4", "Solid phone");

        Assert.Equal(ProductPage, Location(response));
        var html = await PageAsync(ProductPage);
        Assert.Equal("Thank you for your review.", Notice(html, "status"));
        Assert.Contains("Solid phone", html);
        Assert.Matches("data-review-average[^>]*>\\s*4(\\.0)?\\s*<", html);
        Assert.Contains("1 review", html);
    }

    [Fact]
    public async Task A_rating_outside_one_to_five_is_refused()
    {
        await DeliveredOrderAsync();

        await ReviewAsync("9", "too many stars");

        Assert.Equal("Choose from 1 to 5 stars.", Notice(await PageAsync(ProductPage), "error"));
    }

    [Fact]
    public async Task Staff_reply_and_hide_and_customers_cannot_open_the_staff_page()
    {
        await DeliveredOrderAsync();
        await ReviewAsync("2", "Arrived late");
        Assert.Equal(HttpStatusCode.Redirect, (await Client.GetAsync("/Admin/Reviews")).StatusCode);
        await SignInAsAsync("staff@example.com", UserRole.Employee);
        int reviewId;
        using (var scope = Factory.Services.CreateScope())
            reviewId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Reviews.Select(r => r.Id).SingleAsync();

        Assert.Contains("Arrived late", await PageAsync("/Admin/Reviews"));
        await PostFormAsync($"/Admin/Reviews/{reviewId}/Reply", new() { ["Reply"] = "Sorry, we are on it." }, formPage: "/Admin/Reviews");
        Assert.Equal("Reply saved.", Notice(await PageAsync("/Admin/Reviews"), "status"));
        Assert.Contains("Sorry, we are on it.", await PageAsync(ProductPage));

        await PostFormAsync($"/Admin/Reviews/{reviewId}/Hide", new(), formPage: "/Admin/Reviews");
        Assert.DoesNotContain("Arrived late", await PageAsync(ProductPage));
        Assert.Contains("href=\"/Admin/Reviews\"", await PageAsync("/Admin/Orders"));
    }

    [Fact]
    public async Task Reviewing_without_the_anti_forgery_token_is_refused()
    {
        await DeliveredOrderAsync();

        var response = await Client.PostAsync($"/Reviews/{await ProductIdAsync()}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Rating"] = "5" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
