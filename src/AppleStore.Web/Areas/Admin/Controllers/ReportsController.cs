using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Admin;
using AppleStore.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Use cases 33-35 for staff: the report on screen, printed (Save as PDF from
// the browser) and as an Excel file. Without dates it shows this month so far.
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Employee)}")]
[Route("Admin/Reports")]
public class ReportsController : Controller
{
    private const string EndBeforeStart = "The end date is before the start date.";
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        View(await PageAsync(from, to, ct));

    [HttpGet("Print")]
    public async Task<IActionResult> Print(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        View(await PageAsync(from, to, ct));

    [HttpGet("Export")]
    public async Task<IActionResult> Export(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var page = await PageAsync(from, to, ct);
        if (page.Report is null)
            return View(nameof(Index), page);
        return File(SalesReportExcel.Build(page.Report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sales-{page.From:yyyy-MM-dd}-{page.To:yyyy-MM-dd}.xlsx");
    }

    private async Task<ReportPage> PageAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow + ReportService.ShopOffset);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        var (report, problem) = await _reports.SalesAsync(start, end, ct);
        return new ReportPage(start, end, report, problem == ReportProblem.EndBeforeStart ? EndBeforeStart : null);
    }
}
