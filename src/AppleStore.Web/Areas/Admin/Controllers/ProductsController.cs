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

// Use cases 25-27. Rules live in AdminCatalogService; this turns its results
// into the form again (a mistake to fix) or a message on the product page.
[Area("Admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("Admin/Products")]
public class ProductsController : Controller
{
    private readonly IAdminCatalogService _catalog;
    private readonly IImageLibrary _images;
    private readonly AppDbContext _db;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IAdminCatalogService catalog, IImageLibrary images, AppDbContext db, ILogger<ProductsController> logger)
    {
        _catalog = catalog;
        _images = images;
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        ViewData["Search"] = search;
        return View(await _catalog.ListAsync(search, ct));
    }

    [HttpGet("New")]
    public async Task<IActionResult> New(CancellationToken ct) =>
        View("Edit", await PageAsync(null, new ProductForm(), null, [], null, ct));

    [HttpPost("New"), ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ProductForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Edit", await PageAsync(null, form, null, [], AdminMessages.UnreadableNumber, ct));
        var result = await RunAsync(() => _catalog.CreateAsync(form.ToInput(), ct), "create a product");
        if (result is null)
            return View("Edit", await PageAsync(null, form, null, [], AdminMessages.SaveFailed, ct));
        if (result.Outcome != AdminCatalogOutcome.Done)
            return View("Edit", await PageAsync(null, form, null, [], AdminMessages.For(result), ct));

        TempData[CartMessages.StatusKey] = AdminMessages.ProductSaved;
        return RedirectToAction(nameof(Edit), new { id = result.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var product = await _catalog.GetAsync(id, ct);
        if (product is null)
            return NotFound();

        var form = new ProductForm
        {
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            BasePrice = product.BasePrice,
            OnSale = product.OnSale,
            ImageUrl = product.ImageUrl,
            Version = product.Version,
        };
        return View(await PageAsync(id, form, product.Slug, product.Variants, null, ct));
    }

    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var current = await _catalog.GetAsync(id, ct);
            return current is null ? NotFound() : View(await PageAsync(id, form, current.Slug, current.Variants, AdminMessages.UnreadableNumber, ct));
        }
        var result = await RunAsync(() => _catalog.UpdateAsync(id, form.ToInput(), form.Version, ct), "save a product");
        if (result?.Outcome == AdminCatalogOutcome.NotFound)
            return NotFound();
        if (result is null || result.Outcome is not (AdminCatalogOutcome.Done or AdminCatalogOutcome.Changed))
        {
            var product = await _catalog.GetAsync(id, ct);
            return View(await PageAsync(id, form, product?.Slug, product?.Variants ?? [], result is null ? AdminMessages.SaveFailed : AdminMessages.For(result), ct));
        }

        // Changed: reload what is there now and say so.
        TempData[result.Outcome == AdminCatalogOutcome.Done ? CartMessages.StatusKey : CartMessages.ErrorKey] =
            result.Outcome == AdminCatalogOutcome.Done ? AdminMessages.ProductSaved : AdminMessages.For(result);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/OffSale"), ValidateAntiForgeryToken]
    public Task<IActionResult> OffSale(int id, CancellationToken ct) => SetOnSaleAsync(id, false, ct);

    [HttpPost("{id:int}/OnSale"), ValidateAntiForgeryToken]
    public Task<IActionResult> OnSale(int id, CancellationToken ct) => SetOnSaleAsync(id, true, ct);

    private async Task<IActionResult> SetOnSaleAsync(int id, bool onSale, CancellationToken ct)
    {
        var result = await RunAsync(() => _catalog.SetOnSaleAsync(id, onSale, ct), "change a product's sale");
        if (result?.Outcome == AdminCatalogOutcome.NotFound)
            return NotFound();
        return Back(id, result, onSale ? AdminMessages.OnSale : AdminMessages.OffSale);
    }

    [HttpPost("{id:int}/Variants"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariant(int id, VariantAddForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Unreadable(id);
        var input = new VariantInput(form.Sku, form.Configuration, form.Color, form.Region, form.Price, form.StockQty, form.OnSale);
        var result = await RunAsync(() => _catalog.AddVariantAsync(id, input, ct: ct), "add a variant");
        if (result?.Outcome == AdminCatalogOutcome.NotFound)
            return NotFound();
        return Back(id, result, AdminMessages.VariantAdded);
    }

    [HttpPost("Variants/{variantId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeVariant(int variantId, VariantChangeForm form, CancellationToken ct)
    {
        var productId = await _db.ProductVariants.Where(v => v.Id == variantId).Select(v => (int?)v.ProductId).FirstOrDefaultAsync(ct);
        if (productId is null)
            return NotFound();
        if (!ModelState.IsValid)
            return Unreadable(productId.Value);
        var change = new VariantChange(form.Price, form.StockQty, form.OnSale, form.SeenStock, form.Version);
        var result = await RunAsync(() => _catalog.UpdateVariantAsync(variantId, change, ct: ct), "save a variant");
        return Back(productId.Value, result, AdminMessages.VariantSaved);
    }

    private RedirectToActionResult Unreadable(int productId)
    {
        TempData[CartMessages.ErrorKey] = AdminMessages.UnreadableNumber;
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    private RedirectToActionResult Back(int productId, AdminCatalogResult? result, string done)
    {
        if (result?.Outcome == AdminCatalogOutcome.Done)
            TempData[CartMessages.StatusKey] = done;
        else
            TempData[CartMessages.ErrorKey] = result is null ? AdminMessages.SaveFailed : AdminMessages.For(result);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    private async Task<AdminCatalogResult?> RunAsync(Func<Task<AdminCatalogResult>> call, string what)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Admin could not {What}", what);
            return null;
        }
    }

    private async Task<ProductPage> PageAsync(int? id, ProductForm form, string? slug, IReadOnlyList<VariantEdit> variants, string? error, CancellationToken ct)
    {
        var categories = (await _db.Categories.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct))
            .Select(c => (c.Id, c.Name)).ToList();
        return new ProductPage(id, form, slug, variants, categories, _images.All(), error);
    }
}
