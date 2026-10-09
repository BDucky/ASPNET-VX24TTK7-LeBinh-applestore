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

// The pages for one kind of Vouchers row: code vouchers (use cases 29-31,
// VouchersController) and automatic promotions (use case 28,
// PromotionsController). A subclass only names its kind, its address and who
// may use it; every action passes the kind on, so one page never reaches the
// other kind's rows. The views live under Views/Vouchers.
public abstract class DiscountAdminController : Controller
{
    private const string EditView = "~/Areas/Admin/Views/Vouchers/Edit.cshtml";
    private readonly IAdminVoucherService _vouchers;
    private readonly AppDbContext _db;
    private readonly ILogger _logger;

    protected DiscountAdminController(IAdminVoucherService vouchers, AppDbContext db, ILogger logger)
    {
        _vouchers = vouchers;
        _db = db;
        _logger = logger;
    }

    protected abstract VoucherKind Kind { get; }

    private DiscountPageText Text => DiscountPageText.For(Kind);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData[nameof(VoucherKind)] = Kind;
        return View("~/Areas/Admin/Views/Vouchers/Index.cshtml", await _vouchers.ListAsync(Kind, ct));
    }

    [HttpGet("New")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        var start = DateTime.Now.Date;
        return View(EditView, await PageAsync(null, new VoucherForm { StartsAt = start, EndsAt = start.AddMonths(1) }, 0, null, ct));
    }

    [HttpPost("New"), ValidateAntiForgeryToken]
    public async Task<IActionResult> New(VoucherForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(EditView, await PageAsync(null, form, 0, AdminMessages.UnreadableNumber, ct));
        var result = await RunAsync(() => _vouchers.CreateAsync(form.ToInput(Kind), ct));
        if (result?.Outcome != VoucherAdminOutcome.Done)
            return View(EditView, await PageAsync(null, form, 0, result is null ? AdminMessages.SaveFailed : AdminMessages.For(result.Outcome), ct));

        TempData[CartMessages.StatusKey] = Text.Saved;
        return RedirectToAction(nameof(Edit), new { id = result.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var voucher = await _vouchers.GetAsync(id, Kind, ct);
        return voucher is null ? NotFound() : View(EditView, await PageAsync(id, VoucherForm.From(voucher), voucher.UsedCount, null, ct));
    }

    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VoucherForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(EditView, await PageAsync(id, form, (await _vouchers.GetAsync(id, Kind, ct))?.UsedCount ?? 0, AdminMessages.UnreadableNumber, ct));
        var result = await RunAsync(() => _vouchers.UpdateAsync(id, form.ToInput(Kind), form.Version, ct));
        if (result?.Outcome == VoucherAdminOutcome.NotFound)
            return NotFound();
        if (result?.Outcome is VoucherAdminOutcome.Done or VoucherAdminOutcome.Changed)
        {
            TempData[result.Outcome == VoucherAdminOutcome.Done ? CartMessages.StatusKey : CartMessages.ErrorKey] =
                result.Outcome == VoucherAdminOutcome.Done ? Text.Saved : AdminMessages.For(result.Outcome);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var used = (await _vouchers.GetAsync(id, Kind, ct))?.UsedCount ?? 0;
        return View(EditView, await PageAsync(id, form, used, result is null ? AdminMessages.SaveFailed : AdminMessages.For(result.Outcome), ct));
    }

    [HttpPost("{id:int}/Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await RunAsync(() => _vouchers.DeleteAsync(id, Kind, ct));
        if (result?.Outcome == VoucherAdminOutcome.NotFound)
            return NotFound();
        if (result is null)
        {
            TempData[CartMessages.ErrorKey] = AdminMessages.SaveFailed;
            return RedirectToAction(nameof(Edit), new { id });
        }
        TempData[CartMessages.StatusKey] = Text.Deleted;
        return RedirectToAction(nameof(Index));
    }

    private async Task<VoucherAdminResult?> RunAsync(Func<Task<VoucherAdminResult>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Staff could not save a {Kind} row", Kind);
            return null;
        }
    }

    private async Task<VoucherPage> PageAsync(int? id, VoucherForm form, int used, string? error, CancellationToken ct)
    {
        var products = (await _db.Products.OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(ct)).Select(p => (p.Id, p.Name)).ToList();
        return new VoucherPage(id, form, used, products, error, Kind);
    }
}
