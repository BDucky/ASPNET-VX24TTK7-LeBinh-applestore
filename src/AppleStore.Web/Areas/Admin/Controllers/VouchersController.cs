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

// Use cases 29-31.
[Area("Admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("Admin/Vouchers")]
public class VouchersController : Controller
{
    private readonly IAdminVoucherService _vouchers;
    private readonly AppDbContext _db;
    private readonly ILogger<VouchersController> _logger;

    public VouchersController(IAdminVoucherService vouchers, AppDbContext db, ILogger<VouchersController> logger)
    {
        _vouchers = vouchers;
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _vouchers.ListAsync(ct));

    [HttpGet("New")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        var start = DateTime.Now.Date;
        return View("Edit", await PageAsync(null, new VoucherForm { StartsAt = start, EndsAt = start.AddMonths(1) }, 0, null, ct));
    }

    [HttpPost("New"), ValidateAntiForgeryToken]
    public async Task<IActionResult> New(VoucherForm form, CancellationToken ct)
    {
        var result = await RunAsync(() => _vouchers.CreateAsync(form.ToInput(), ct));
        if (result?.Outcome != VoucherAdminOutcome.Done)
            return View("Edit", await PageAsync(null, form, 0, result is null ? AdminMessages.SaveFailed : AdminMessages.For(result.Outcome), ct));

        TempData[CartMessages.StatusKey] = AdminMessages.VoucherSaved;
        return RedirectToAction(nameof(Edit), new { id = result.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var voucher = await _vouchers.GetAsync(id, ct);
        return voucher is null ? NotFound() : View(await PageAsync(id, VoucherForm.From(voucher), voucher.UsedCount, null, ct));
    }

    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VoucherForm form, CancellationToken ct)
    {
        var result = await RunAsync(() => _vouchers.UpdateAsync(id, form.ToInput(), form.Version, ct));
        if (result?.Outcome == VoucherAdminOutcome.NotFound)
            return NotFound();
        if (result?.Outcome is VoucherAdminOutcome.Done or VoucherAdminOutcome.Changed)
        {
            TempData[result.Outcome == VoucherAdminOutcome.Done ? CartMessages.StatusKey : CartMessages.ErrorKey] =
                result.Outcome == VoucherAdminOutcome.Done ? AdminMessages.VoucherSaved : AdminMessages.For(result.Outcome);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var used = (await _vouchers.GetAsync(id, ct))?.UsedCount ?? 0;
        return View(await PageAsync(id, form, used, result is null ? AdminMessages.SaveFailed : AdminMessages.For(result.Outcome), ct));
    }

    [HttpPost("{id:int}/Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await RunAsync(() => _vouchers.DeleteAsync(id, ct));
        if (result?.Outcome == VoucherAdminOutcome.NotFound)
            return NotFound();
        if (result is null)
        {
            TempData[CartMessages.ErrorKey] = AdminMessages.SaveFailed;
            return RedirectToAction(nameof(Edit), new { id });
        }
        TempData[CartMessages.StatusKey] = AdminMessages.VoucherDeleted;
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
            _logger.LogError(ex, "Admin could not save a voucher");
            return null;
        }
    }

    private async Task<VoucherPage> PageAsync(int? id, VoucherForm form, int used, string? error, CancellationToken ct)
    {
        var products = (await _db.Products.OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(ct)).Select(p => (p.Id, p.Name)).ToList();
        return new VoucherPage(id, form, used, products, error);
    }
}
