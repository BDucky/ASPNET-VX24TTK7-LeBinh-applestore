using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Products;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// Attribute-routed (not the conventional {controller}/{action}/{id?} route in
// Program.cs) so product pages get clean paths: /Products and
// /Products/{slug}, not /Products/Details?slug=.
[Route("Products")]
public class ProductsController : Controller
{
    private readonly IProductCatalogService _catalog;

    public ProductsController(IProductCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? category, string? q, ProductSort sort = ProductSort.Featured, CancellationToken ct = default)
    {
        var products = await _catalog.GetProductsAsync(category, q, sort, ct);
        return View(new ProductListViewModel(products, category, q, sort));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var product = await _catalog.GetBySlugAsync(slug, ct);
        if (product is null)
            return NotFound();

        await LoadModelStripAsync(product.CategorySlug, product.Slug, ct);
        return View(product);
    }

    [HttpGet("{slug}/{sku}")]
    public async Task<IActionResult> Variant(string slug, string sku, CancellationToken ct)
    {
        var variant = await _catalog.GetVariantAsync(slug, sku, ct);
        if (variant is null)
            return NotFound();

        await LoadModelStripAsync(variant.CategorySlug, variant.ProductSlug, ct);
        return View(variant);
    }

    // One owner for the model strip both the model and variant pages show.
    private async Task LoadModelStripAsync(string categorySlug, string activeSlug, CancellationToken ct)
    {
        var models = await _catalog.GetProductsAsync(categorySlug, ct: ct);
        ViewData[ModelStripViewModel.ViewDataKey] = new ModelStripViewModel(models, activeSlug);
    }
}
