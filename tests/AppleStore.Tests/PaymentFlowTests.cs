using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Helpers for the payment tests: a shopper ready to check out, placing,
// and paying at the simulator. Holds no tests itself.
public abstract class PaymentFlowBase : WebFlowTestBase
{
    protected const string Password = "Password1";

    protected PaymentFlowBase()
    {
    }

    protected PaymentFlowBase(AppleStoreWebFactory factory) : base(factory)
    {
    }

    protected async Task<string> PageAsync(string url) => WebUtility.HtmlDecode(await Client.GetStringAsync(url));

    protected static string Location(HttpResponseMessage response) => response.Headers.Location?.OriginalString ?? "";

    protected static string Notice(string html, string kind) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"class=\"cart-{kind}\"[^>]*>\\s*([^<]+?)\\s*<").Groups[1].Value);

    private static string Field(string html, string name) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

    // Signs in a new shopper with one phone in the cart and opens checkout.
    protected async Task<string> ReadyToCheckOutAsync(string email = "payer@example.com")
    {
        var (blue, _, _) = Seed();
        await CreateUserAsync(email, Password);
        await LoginAsync(email, Password);
        await PostFormAsync("/Cart/Add", new() { ["VariantId"] = blue.ToString(), ["Quantity"] = "1", ["ReturnUrl"] = "/Cart" }, formPage: PhoneVariantUrl);
        return await PageAsync("/Checkout");
    }

    protected Task<HttpResponseMessage> PlaceAsync(string page, string method) =>
        PostFormAsync("/Checkout", new()
        {
            ["FullName"] = "Payer",
            ["Phone"] = "0911111111",
            ["AddressLine"] = "1 Street",
            ["ExpectedTotal"] = Field(page, "ExpectedTotal"),
            ["PaymentMethod"] = method,
            ["Intent"] = "place",
        }, formPage: "/Checkout");

    protected async Task<int> OnlyOrderIdAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Select(o => o.Id).SingleAsync();
    }

    protected async Task<OrderPaymentStatus> PaymentStatusAsync(int orderId)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == orderId).Select(o => o.PaymentStatus).SingleAsync();
    }

    // Opens the simulator page, presses one of its buttons, and follows the
    // gateway's return to the order page.
    protected async Task<(HttpResponseMessage Return, string OrderPage)> PayAsync(string simulatorUrl, string result)
    {
        var simulator = await PageAsync(simulatorUrl);
        var fields = Regex.Matches(simulator, "<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        fields.Remove("__RequestVerificationToken");
        fields["result"] = result;
        var answered = await PostFormAsync("/PaymentSimulator", fields, formPage: simulatorUrl);
        Assert.StartsWith("/Payments/Return?", Location(answered));
        var returned = await Client.GetAsync(Location(answered));
        return (returned, await PageAsync(Location(returned)));
    }

}

// Online payment through the real app in Development, where
// appsettings.Development.json turns the simulated gateway on.
public class PaymentFlowTests : PaymentFlowBase
{
    // ---------- Checkout ----------

    [Fact]
    public async Task Checkout_offers_cash_vnpay_and_momo()
    {
        var page = await ReadyToCheckOutAsync();

        Assert.Contains("value=\"Cod\"", page);
        Assert.Contains("value=\"VnPay\"", page);
        Assert.Contains("value=\"MoMo\"", page);
    }

    [Fact]
    public async Task Placing_with_vnpay_sends_the_shopper_to_the_gateway_with_the_order_unpaid()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "VnPay");

        Assert.StartsWith("/PaymentSimulator?", Location(placed));
        var simulator = await PageAsync(Location(placed));
        Assert.Contains("VNPay", simulator);
        Assert.Contains("24.990.000 VNĐ", simulator);
        Assert.Equal(OrderPaymentStatus.Unpaid, await PaymentStatusAsync(await OnlyOrderIdAsync()));
    }

    [Fact]
    public async Task Cash_on_delivery_still_goes_straight_to_the_order()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "Cod");

        Assert.Matches("^/Orders/\\d+$", Location(placed));
    }

    // ---------- The simulator ----------

    [Fact]
    public async Task Paying_at_the_gateway_marks_the_order_paid()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "MoMo");

        var (returned, order) = await PayAsync(Location(placed), "00");

        var orderId = await OnlyOrderIdAsync();
        Assert.Equal($"/Orders/{orderId}", Location(returned));
        Assert.Equal("Payment received. Thank you.", Notice(order, "status"));
        Assert.Contains("MoMo, paid", order);
        Assert.DoesNotContain("Pay now", order);
        Assert.Equal(OrderPaymentStatus.Paid, await PaymentStatusAsync(orderId));
    }

    [Theory]
    [InlineData("51", "The payment did not go through, and nothing was charged. You can try again.")]
    [InlineData("24", "The payment was cancelled. You can try again.")]
    public async Task A_failed_or_cancelled_payment_says_so_and_can_be_paid_again(string result, string message)
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "VnPay");

        var (_, order) = await PayAsync(Location(placed), result);

        Assert.Equal(message, Notice(order, "error"));
        Assert.Contains("Pay now", order);
        var orderId = await OnlyOrderIdAsync();
        var retry = await PostFormAsync($"/Payments/Pay/{orderId}", new(), formPage: $"/Orders/{orderId}");
        Assert.StartsWith("/PaymentSimulator?", Location(retry));
        var (_, paid) = await PayAsync(Location(retry), "00");
        Assert.Equal("Payment received. Thank you.", Notice(paid, "status"));
        Assert.Equal(OrderPaymentStatus.Paid, await PaymentStatusAsync(orderId));
    }

    [Fact]
    public async Task Coming_back_to_the_same_return_address_changes_nothing()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "VnPay");
        var simulator = await PageAsync(Location(placed));
        var fields = Regex.Matches(simulator, "<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        fields.Remove("__RequestVerificationToken");
        fields["result"] = "00";
        var answered = await PostFormAsync("/PaymentSimulator", fields, formPage: Location(placed));
        await Client.GetAsync(Location(answered));

        var again = await Client.GetAsync(Location(answered));

        Assert.Equal("This order is already paid.", Notice(await PageAsync(Location(again)), "status"));
    }

    [Fact]
    public async Task A_tampered_gateway_link_is_refused_with_a_way_back()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "VnPay");
        var tampered = Regex.Replace(Location(placed), "amount=\\d+", "amount=1");

        var response = await Client.GetAsync(tampered);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("This payment link is not valid.", html);
        Assert.Contains("href=\"/\"", html);
    }

    [Fact]
    public async Task A_tampered_return_is_refused_and_the_order_stays_unpaid()
    {
        var placed = await PlaceAsync(await ReadyToCheckOutAsync(), "VnPay");
        var orderId = await OnlyOrderIdAsync();
        var paymentId = Regex.Match(Location(placed), "paymentId=(\\d+)").Groups[1].Value;

        var response = await Client.GetAsync($"/Payments/Return?paymentId={paymentId}&amount=24990000&result=00&txnId=X&signature=00");

        Assert.Equal("/", Location(response));
        Assert.Equal("We could not confirm this payment. If money was taken, contact us with your order number.", Notice(await PageAsync("/"), "error"));
        Assert.Equal(OrderPaymentStatus.Unpaid, await PaymentStatusAsync(orderId));
    }

    [Fact]
    public async Task Another_user_cannot_start_a_payment_for_an_order()
    {
        await PlaceAsync(await ReadyToCheckOutAsync("owner@example.com"), "VnPay");
        var orderId = await OnlyOrderIdAsync();
        await PostFormAsync("/Account/Logout", new(), formPage: "/");
        await CreateUserAsync("other@example.com", Password);
        await LoginAsync("other@example.com", Password);

        var response = await PostFormAsync($"/Payments/Pay/{orderId}", new(), formPage: "/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Starting_a_payment_without_the_anti_forgery_token_is_refused()
    {
        await PlaceAsync(await ReadyToCheckOutAsync(), "VnPay");

        var response = await Client.PostAsync($"/Payments/Pay/{await OnlyOrderIdAsync()}", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// Payments:Mode not set: only cash on delivery, and no simulator.
public class PaymentsOffTests : PaymentFlowBase
{
    public PaymentsOffTests() : base(new AppleStoreWebFactory(new Dictionary<string, string?> { ["Payments:Mode"] = "" }))
    {
    }

    [Fact]
    public async Task Only_cash_on_delivery_is_offered_and_an_online_method_is_refused()
    {
        var page = await ReadyToCheckOutAsync();

        var placed = await PlaceAsync(page, "VnPay");

        Assert.DoesNotContain("value=\"VnPay\"", page);
        Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        Assert.Contains("This payment method is not available.", WebUtility.HtmlDecode(await placed.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/PaymentSimulator?paymentId=1")).StatusCode);
    }
}

public class PaymentStartupTests
{
    [Theory]
    [InlineData("Production", "Simulated")]
    [InlineData("Development", "Sandbox")]
    public void The_app_refuses_to_start_with_the_simulator_in_production_or_an_unknown_mode(string environment, string mode)
    {
        using var factory = new AppleStoreWebFactory(new Dictionary<string, string?> { ["Payments:Mode"] = mode }, environment: environment);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Payments:Mode", error.ToString());
    }
}
