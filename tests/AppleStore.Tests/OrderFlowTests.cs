using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Orders after checkout, through the real app: the customer's pages, the
// public tracking page, and the staff pages in the Admin area.
public class OrderFlowTests : PaymentFlowBase
{
    private async Task<int> PlaceCashOrderAsync(string email = "buyer@example.com")
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(email), "Cod");
        Assert.Matches("^/Orders/\\d+$", Location(placed));
        return int.Parse(Location(placed).Split('/')[^1]);
    }

    private async Task SignInAsAsync(string email, UserRole role)
    {
        await PostFormAsync("/Account/Logout", new(), formPage: "/");
        await CreateUserAsync(email, Password, role: role);
        await LoginAsync(email, Password);
    }

    private async Task<(OrderStatus Status, OrderPaymentStatus Paid, int Stock)> StateAsync(int orderId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        var variant = await db.OrderItems.Where(i => i.OrderId == orderId).Select(i => i.Variant.StockQty).FirstAsync();
        return (order.Status, order.PaymentStatus, variant);
    }

    private Task<HttpResponseMessage> StaffAsync(int orderId, string action, Dictionary<string, string>? fields = null) =>
        PostFormAsync($"/Admin/Orders/{orderId}/{action}", fields ?? new(), formPage: $"/Admin/Orders/{orderId}");

    private const string Phone = "0911111111";

    // ---------- The customer ----------

    [Fact]
    public async Task Placing_an_order_emails_the_customer()
    {
        var order = await PlaceCashOrderAsync();

        var mail = Assert.Single(Factory.Email.Sent, m => m.Subject == $"Order #{order} received");
        Assert.Equal("buyer@example.com", mail.To);
        Assert.Contains($"/Orders/{order}", mail.Body);
    }

    [Fact]
    public async Task My_orders_lists_only_mine_newest_first()
    {
        var first = await PlaceCashOrderAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.ProductVariants.ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, 9));
        }
        await PostFormAsync("/Cart/Add", new() { ["VariantId"] = (await FirstVariantAsync()).ToString(), ["Quantity"] = "1", ["ReturnUrl"] = "/Cart" }, formPage: PhoneVariantUrl);
        var second = int.Parse(Location(await PlaceAsync(await PageAsync("/Checkout"), "Cod")).Split('/')[^1]);

        var html = await PageAsync("/Orders");

        Assert.Equal([$"/Orders/{second}", $"/Orders/{first}"], Regex.Matches(html, "href=\"(/Orders/\\d+)\"").Select(m => m.Groups[1].Value).Distinct());
        await SignInAsAsync("someone@example.com", UserRole.Customer);
        Assert.Contains("You have no orders yet.", await PageAsync("/Orders"));
    }

    private async Task<int> FirstVariantAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants.Where(v => v.StockQty > 0).Select(v => v.Id).FirstAsync();
    }

    [Fact]
    public async Task A_customer_cancels_a_pending_order_and_the_stock_comes_back()
    {
        var order = await PlaceCashOrderAsync();
        Assert.Contains("Cancel this order", await PageAsync($"/Orders/{order}"));
        var stockAfterOrder = (await StateAsync(order)).Stock;

        var response = await PostFormAsync($"/Orders/{order}/Cancel", new(), formPage: $"/Orders/{order}");

        Assert.Equal($"/Orders/{order}", Location(response));
        var page = await PageAsync($"/Orders/{order}");
        Assert.Equal("Your order was cancelled.", Notice(page, "status"));
        Assert.DoesNotContain("Cancel this order", page);
        Assert.Equal((OrderStatus.Cancelled, stockAfterOrder + 1), ((await StateAsync(order)).Status, (await StateAsync(order)).Stock));
    }

    [Fact]
    public async Task A_customer_cannot_cancel_once_staff_confirmed()
    {
        var order = await PlaceCashOrderAsync();
        var customerPage = await PageAsync($"/Orders/{order}");
        await SignInAsAsync("staff@example.com", UserRole.Employee);
        await StaffAsync(order, "Confirm");
        await SignInAsAsync("buyer2@example.com", UserRole.Customer);

        Assert.Equal(HttpStatusCode.NotFound, (await PostFormAsync($"/Orders/{order}/Cancel", new(), formPage: "/")).StatusCode);
        Assert.Contains("Cancel this order", customerPage);
        Assert.Equal(OrderStatus.Confirmed, (await StateAsync(order)).Status);
    }

    [Fact]
    public async Task The_owner_is_told_when_a_cancel_comes_too_late()
    {
        var order = await PlaceCashOrderAsync();
        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == order)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Confirmed));

        await PostFormAsync($"/Orders/{order}/Cancel", new(), formPage: "/Orders");

        var page = await PageAsync($"/Orders/{order}");
        Assert.Equal("This order can no longer be cancelled. Contact us if you need help.", Notice(page, "error"));
        Assert.DoesNotContain("Cancel this order", page);
    }

    // ---------- Tracking without signing in ----------

    [Fact]
    public async Task Anyone_with_the_order_number_and_phone_sees_status_and_tracking_but_no_address_or_price()
    {
        var order = await PlaceCashOrderAsync();
        await SignInAsAsync("staff@example.com", UserRole.Employee);
        await StaffAsync(order, "Confirm");
        await StaffAsync(order, "Ship", new() { ["Carrier"] = "GHN", ["TrackingNo"] = "GHN777" });
        await PostFormAsync("/Account/Logout", new(), formPage: "/");

        var response = await PostFormAsync("/Track", new() { ["OrderId"] = order.ToString(), ["Phone"] = "0911 111 111" });

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("On the way", html);
        Assert.Contains("GHN777", html);
        Assert.DoesNotContain("1 Street", html);
        Assert.DoesNotContain("VNĐ", html.Split("data-track-result")[1]);
    }

    [Fact]
    public async Task Every_page_links_to_tracking()
    {
        Assert.Contains("href=\"/Track\"", await Client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Tracking_with_the_wrong_phone_finds_nothing()
    {
        var order = await PlaceCashOrderAsync();
        await PostFormAsync("/Account/Logout", new(), formPage: "/");

        var response = await PostFormAsync("/Track", new() { ["OrderId"] = order.ToString(), ["Phone"] = "0999999999" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No order matches that number and phone.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    // ---------- Staff ----------

    [Fact]
    public async Task Staff_move_a_cash_order_from_pending_to_delivered_and_paid()
    {
        var order = await PlaceCashOrderAsync();
        await SignInAsAsync("staff@example.com", UserRole.Employee);

        var list = await PageAsync("/Admin/Orders");
        Assert.Contains($"href=\"/Admin/Orders/{order}\"", list);

        Assert.Equal($"/Admin/Orders/{order}", Location(await StaffAsync(order, "Confirm")));
        Assert.Equal("Order confirmed.", Notice(await PageAsync($"/Admin/Orders/{order}"), "status"));

        await StaffAsync(order, "Ship", new() { ["Carrier"] = " ", ["TrackingNo"] = "" });
        Assert.Equal("Enter the carrier and the tracking number.", Notice(await PageAsync($"/Admin/Orders/{order}"), "error"));

        await StaffAsync(order, "Ship", new() { ["Carrier"] = "GHN", ["TrackingNo"] = "GHN777" });
        var shipped = Assert.Single(Factory.Email.Sent, m => m.Subject == $"Order #{order} is on its way");
        Assert.Contains("GHN777", shipped.Body);

        await StaffAsync(order, "Complete");
        Assert.Equal((OrderStatus.Completed, OrderPaymentStatus.Paid), ((await StateAsync(order)).Status, (await StateAsync(order)).Paid));
        var page = await PageAsync($"/Admin/Orders/{order}");
        Assert.DoesNotContain("value=\"Confirm\"", page);
        Assert.DoesNotContain("<form", page.Split("data-order-actions")[1].Split("</section>")[0]);
    }

    [Fact]
    public async Task Staff_see_only_the_buttons_the_rules_allow_and_why()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "VnPay");
        var order = int.Parse(Regex.Match(Location(placed), "orderId=(\\d+)").Groups[1].Value);
        await SignInAsAsync("staff@example.com", UserRole.Employee);

        var page = await PageAsync($"/Admin/Orders/{order}");

        Assert.Contains("Waiting for payment before it can be confirmed.", page);
        Assert.DoesNotContain($"action=\"/Admin/Orders/{order}/Confirm\"", page);
        Assert.Contains($"action=\"/Admin/Orders/{order}/Cancel\"", page);
    }

    [Fact]
    public async Task A_stale_staff_page_is_told_the_order_changed()
    {
        var order = await PlaceCashOrderAsync();
        await SignInAsAsync("staff@example.com", UserRole.Employee);
        await StaffAsync(order, "Confirm");

        await StaffAsync(order, "Confirm");

        Assert.Equal("This order changed meanwhile. Check it and try again.", Notice(await PageAsync($"/Admin/Orders/{order}"), "error"));
    }

    [Fact]
    public async Task Staff_cancelling_a_paid_order_are_told_to_refund()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "MoMo");
        var (_, _) = await PayAsync(Location(placed), "00");
        var order = await OnlyOrderIdAsync();
        await SignInAsAsync("staff@example.com", UserRole.Employee);

        await StaffAsync(order, "Cancel");

        Assert.Equal("Order cancelled. It was paid: refund the customer.", Notice(await PageAsync($"/Admin/Orders/{order}"), "status"));
    }

    [Fact]
    public async Task Staff_filter_the_list_by_status()
    {
        var first = await PlaceCashOrderAsync();
        await SignInAsAsync("staff@example.com", UserRole.Employee);
        await StaffAsync(first, "Confirm");

        Assert.Contains($"href=\"/Admin/Orders/{first}\"", await PageAsync("/Admin/Orders?status=Confirmed"));
        Assert.DoesNotContain($"href=\"/Admin/Orders/{first}\"", await PageAsync("/Admin/Orders?status=Pending"));
    }

    [Theory]
    [InlineData(UserRole.Customer, "/Admin/Orders", false)]
    [InlineData(UserRole.Employee, "/Admin/Orders", true)]
    [InlineData(UserRole.Employee, "/Admin", false)]
    [InlineData(UserRole.Admin, "/Admin/Orders", true)]
    public async Task Order_pages_are_for_staff_and_the_dashboard_for_admins(UserRole role, string path, bool allowed)
    {
        await CreateUserAsync("who@example.com", Password, role: role);
        await LoginAsync("who@example.com", Password);

        var response = await Client.GetAsync(path);

        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
        if (!allowed)
        {
            var location = response.Headers.Location!;
            Assert.StartsWith("/Account/AccessDenied", location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString);
        }
    }

    [Theory]
    [InlineData(UserRole.Employee, "Staff", "/Admin/Orders")]
    [InlineData(UserRole.Admin, "Admin", "/Admin")]
    public async Task Staff_get_a_link_to_their_pages_in_the_nav(UserRole role, string label, string href)
    {
        await CreateUserAsync("who@example.com", Password, role: role);
        await LoginAsync("who@example.com", Password);

        Assert.Matches($"href=\"{href}\"[^>]*>{label}</a>", await Client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Access_denied_no_longer_says_staff_only()
    {
        var page = await PageAsync("/Account/AccessDenied");

        Assert.DoesNotContain("staff accounts only", page);
        Assert.Contains("href=\"/\"", page);
    }
}
