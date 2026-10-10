using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models.Admin;
using AppleStore.Web.Models.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Use case 21: selling at the counter. The report gives in-person sales to
// employees, so both staff roles may use it. Like the stock form, the form's
// key is in its address, so Back or a second press never sells twice.
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Employee)}")]
[Route("Admin/Sales")]
public class SalesController : Controller
{
    private readonly IInStoreSaleService _sales;
    private readonly IStockService _stock;
    private readonly ILogger<SalesController> _logger;

    public SalesController(IInStoreSaleService sales, IStockService stock, ILogger<SalesController> logger)
    {
        _sales = sales;
        _stock = stock;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(New));

    [HttpGet("New")]
    public async Task<IActionResult> New(Guid? key, CancellationToken ct)
    {
        if (key is not { } formKey || formKey == Guid.Empty)
            return RedirectToAction(nameof(New), new { key = Guid.NewGuid() });
        if (await _sales.OrderForKeyAsync(formKey, ct) is { } sold)
            return ToSale(sold, AdminMessages.SaleAlreadyDone);
        return View(await PageAsync(new SaleForm { FormKey = formKey, Lines = [new()] }, null, null, ct));
    }

    [HttpPost("New"), ValidateAntiForgeryToken]
    public async Task<IActionResult> New(SaleForm form, CancellationToken ct)
    {
        if (form.FormKey == Guid.Empty)
            return RedirectToAction(nameof(New));
        if (!ModelState.IsValid)
            return View(await PageAsync(form, null, AdminMessages.UnreadableNumber, ct));
        try
        {
            if (form.Intent == "sell")
            {
                var result = await _sales.SellAsync(form.ToInput(), form.ExpectedTotal ?? -1m, User.AccountId()!.Value, ct);
                if (result.Outcome is SaleOutcome.Done or SaleOutcome.AlreadySold)
                    return ToSale(result.OrderId!.Value, result.Outcome == SaleOutcome.Done ? AdminMessages.SaleDone : AdminMessages.SaleAlreadyDone);
                var fresh = await _sales.QuoteAsync(form.FilledLines, form.VoucherCode, ct);
                return View(await PageAsync(form, fresh, AdminMessages.For(result.Outcome, result.Sku, result.VoucherProblem), ct));
            }
            var quote = await _sales.QuoteAsync(form.FilledLines, form.VoucherCode, ct);
            var problem = AdminMessages.For(quote.Problem, quote.ProblemSku, VoucherProblem.None)
                ?? (quote.VoucherCode is not null && quote.VoucherProblem != VoucherProblem.None ? AdminMessages.Voucher(quote.VoucherProblem) : null);
            return View(await PageAsync(form, quote, problem, ct));
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Staff could not complete a counter sale");
            return View(await PageAsync(form, null, AdminMessages.SaveFailed, ct));
        }
    }

    private RedirectResult ToSale(int orderId, string message)
    {
        TempData[CartMessages.StatusKey] = message;
        return Redirect($"/Admin/Orders/{orderId}");
    }

    // On-sale variants to pick from, by product name. The total to complete
    // at is the one just shown.
    private async Task<SalePage> PageAsync(SaleForm form, SaleQuote? quote, string? error, CancellationToken ct)
    {
        var variants = (await _stock.LevelsAsync(null, ct)).Where(v => v.OnSale).OrderBy(v => v.ProductName).ThenBy(v => v.Sku).ToList();
        if (form.Lines.Count == 0)
            form.Lines.Add(new SaleLineForm());
        form.ExpectedTotal = quote is { Problem: SaleOutcome.Done } ? quote.Total : null;
        ModelState.Remove(nameof(SaleForm.ExpectedTotal));
        return new SalePage(form, variants, quote, error);
    }
}
