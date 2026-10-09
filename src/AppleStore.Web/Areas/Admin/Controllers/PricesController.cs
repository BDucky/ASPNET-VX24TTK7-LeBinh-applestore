using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models.Admin;
using AppleStore.Web.Models.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Web.Areas.Admin.Controllers;

// BM_PRICE_01: batch price changes and the price history. The report gives
// price updates to employees as well as admins (owner's choice 2026-10-09).
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Employee)}")]
[Route("Admin/Prices")]
public class PricesController : Controller
{
    private readonly IPriceService _prices;
    private readonly AppDbContext _db;
    private readonly ILogger<PricesController> _logger;

    public PricesController(IPriceService prices, AppDbContext db, ILogger<PricesController> logger)
    {
        _prices = prices;
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? product, CancellationToken ct) => View(await PageAsync(new PriceBatchForm(), product, null, ct));

    [HttpPost("Batch"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Batch(PriceBatchForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid || !form.AmountIsWhole)
            return View(nameof(Index), await PageAsync(form, null, AdminMessages.UnreadableNumber, ct));
        PriceBatchResult result;
        try
        {
            result = await _prices.ApplyBatchAsync(form.ToInput(), User.AccountId()!.Value, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Staff could not change prices");
            return View(nameof(Index), await PageAsync(form, null, AdminMessages.SaveFailed, ct));
        }
        if (result.Outcome != PriceBatchOutcome.Done)
            return View(nameof(Index), await PageAsync(form, null, AdminMessages.For(result), ct));

        TempData[CartMessages.StatusKey] = AdminMessages.For(result);
        return RedirectToAction(nameof(Index));
    }

    private async Task<PricesPage> PageAsync(PriceBatchForm form, int? product, string? error, CancellationToken ct)
    {
        var products = (await _db.Products.OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(ct)).Select(p => (p.Id, p.Name)).ToList();
        var categories = (await _db.Categories.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct)).Select(c => (c.Id, c.Name)).ToList();
        return new PricesPage(form, products, categories, await _prices.HistoryAsync(product, ct), product, error);
    }
}
