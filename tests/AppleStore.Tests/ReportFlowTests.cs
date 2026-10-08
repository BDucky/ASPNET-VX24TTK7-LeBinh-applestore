using System.Net;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// The report pages, the Excel export and the invoices through the real app.
public class ReportFlowTests : PaymentFlowBase
{
    private async Task<int> PaidOrderAsync()
    {
        await PlaceAsync(await ReadyToCheckOutAsync(), "Cod");
        var order = await OnlyOrderIdAsync();
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == order)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Completed).SetProperty(o => o.PaymentStatus, OrderPaymentStatus.Paid));
        return order;
    }

    private async Task SignInAsAsync(string email, UserRole role)
    {
        if ((await Client.GetStringAsync("/")).Contains("site-nav-account-out"))
            await PostFormAsync("/Account/Logout", new(), formPage: "/");
        await CreateUserAsync(email, Password, role: role);
        await LoginAsync(email, Password);
    }

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow + TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");

    [Theory]
    [InlineData(UserRole.Customer, false)]
    [InlineData(UserRole.Employee, true)]
    [InlineData(UserRole.Admin, true)]
    public async Task Reports_are_for_staff(UserRole role, bool allowed)
    {
        await CreateUserAsync("who@example.com", Password, role: role);
        await LoginAsync("who@example.com", Password);

        var response = await Client.GetAsync("/Admin/Reports");

        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task The_report_shows_the_revenue_of_a_paid_order_today()
    {
        await PaidOrderAsync();
        await SignInAsAsync("staff@example.com", UserRole.Employee);

        var html = await PageAsync($"/Admin/Reports?from={Today}&to={Today}");

        Assert.Matches("data-report-revenue[^>]*>\\s*24\\.990\\.000 VNĐ\\s*<", html);
        Assert.Contains("iPhone 17", html);
    }

    [Fact]
    public async Task An_end_before_the_start_says_so()
    {
        await SignInAsAsync("admin@example.com", UserRole.Admin);

        var html = await PageAsync("/Admin/Reports?from=2026-11-05&to=2026-11-01");

        Assert.Contains("The end date is before the start date.", html);
    }

    [Fact]
    public async Task The_excel_export_is_a_real_workbook_with_the_numbers()
    {
        await PaidOrderAsync();
        await SignInAsAsync("admin@example.com", UserRole.Admin);

        var response = await Client.GetAsync($"/Admin/Reports/Export?from={Today}&to={Today}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"sales-{Today}-{Today}.xlsx", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        Assert.Equal(["Summary", "By day", "By product"], workbook.Worksheets.Select(w => w.Name));
        var summary = workbook.Worksheet("Summary");
        var revenueRow = summary.RowsUsed().Single(r => r.Cell(1).GetString() == "Revenue");
        Assert.Equal(24_990_000m, revenueRow.Cell(2).GetValue<decimal>());
        Assert.Equal("iPhone 17", workbook.Worksheet("By product").Cell(2, 1).GetString());
    }

    [Fact]
    public async Task A_customer_prints_their_own_invoice_and_not_someone_elses()
    {
        var order = await PaidOrderAsync();

        var invoice = await PageAsync($"/Orders/{order}/Invoice");

        Assert.Contains($"Invoice for order #{order}", invoice);
        Assert.Contains("24.990.000 VNĐ", invoice);
        Assert.DoesNotContain("site-nav", invoice);
        await SignInAsAsync("someone@example.com", UserRole.Customer);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/Orders/{order}/Invoice")).StatusCode);
    }

    [Fact]
    public async Task Staff_print_any_invoice()
    {
        var order = await PaidOrderAsync();
        await SignInAsAsync("staff@example.com", UserRole.Employee);

        Assert.Contains($"Invoice for order #{order}", await PageAsync($"/Admin/Orders/{order}/Invoice"));
        Assert.Contains("href=\"/Admin/Reports\"", await PageAsync("/Admin/Orders"));
    }

    [Fact]
    public async Task The_print_page_of_the_report_has_no_shop_navigation()
    {
        await SignInAsAsync("admin@example.com", UserRole.Admin);

        var html = await PageAsync($"/Admin/Reports/Print?from={Today}&to={Today}");

        Assert.Contains("Sales report", html);
        Assert.DoesNotContain("site-nav", html);
    }
}
