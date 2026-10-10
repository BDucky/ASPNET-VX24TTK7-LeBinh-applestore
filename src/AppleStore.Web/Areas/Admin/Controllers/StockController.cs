using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models.Admin;
using AppleStore.Web.Models.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Use case 20: goods intake receipts and stock levels. The report gives
// intake and stock checks to employees, so both staff roles may use it.
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Employee)}")]
[Route("Admin/Stock")]
public class StockController : Controller
{
    private readonly IStockService _stock;
    private readonly ILogger<StockController> _logger;

    public StockController(IStockService stock, ILogger<StockController> logger)
    {
        _stock = stock;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _stock.ReceiptsAsync(ct));

    // The form's key is in the address (found 2026-10-10: the browser's Back
    // fetched the form again, and a fresh key let the same goods be received
    // twice). Back returns to the same key; a key already used shows its
    // receipt instead of the form.
    [HttpGet("New")]
    public async Task<IActionResult> New(Guid? key, CancellationToken ct)
    {
        if (await this.FreshOrUsedAsync(key, k => _stock.ReceiptForKeyAsync(k, ct), saved =>
            {
                TempData[CartMessages.StatusKey] = AdminMessages.ReceiptAlreadySaved;
                return RedirectToAction(nameof(Receipt), new { id = saved });
            }) is { } elsewhere)
            return elsewhere;
        return View(await PageAsync(new StockReceiptForm { FormKey = key!.Value, Lines = [new()] }, null, ct));
    }

    [HttpPost("New"), ValidateAntiForgeryToken]
    public async Task<IActionResult> New(StockReceiptForm form, CancellationToken ct)
    {
        if (form.FormKey == Guid.Empty)
            return RedirectToAction(nameof(New));
        if (!ModelState.IsValid || !form.CostsAreWhole)
            return View(await PageAsync(form, AdminMessages.UnreadableNumber, ct));
        StockReceiptResult result;
        try
        {
            result = await _stock.ReceiveAsync(form.ToInput(), User.AccountId(), ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Staff could not save a stock receipt");
            return View(await PageAsync(form, AdminMessages.SaveFailed, ct));
        }
        if (result.Outcome is not (StockReceiptOutcome.Done or StockReceiptOutcome.AlreadySaved))
            return View(await PageAsync(form, AdminMessages.For(result), ct));

        TempData[CartMessages.StatusKey] = AdminMessages.For(result);
        return RedirectToAction(nameof(Receipt), new { id = result.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Receipt(int id, CancellationToken ct) =>
        await _stock.ReceiptAsync(id, ct) is { } receipt ? View(receipt) : NotFound();

    [HttpGet("{id:int}/Print")]
    public async Task<IActionResult> Print(int id, CancellationToken ct) =>
        await _stock.ReceiptAsync(id, ct) is { } receipt ? View(receipt) : NotFound();

    [HttpGet("Levels")]
    public async Task<IActionResult> Levels(string? q, CancellationToken ct)
    {
        ViewData["Search"] = q;
        return View(await _stock.LevelsAsync(q, ct));
    }

    // Variants to pick from, by product name.
    private async Task<StockNewPage> PageAsync(StockReceiptForm form, string? error, CancellationToken ct)
    {
        var variants = (await _stock.LevelsAsync(null, ct)).OrderBy(v => v.ProductName).ThenBy(v => v.Sku).ToList();
        if (form.Lines.Count == 0)
            form.Lines.Add(new StockLineForm());
        return new StockNewPage(form, variants, error);
    }
}
