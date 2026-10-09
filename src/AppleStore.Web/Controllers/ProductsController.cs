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
        var products = await _catalog.GetProductsAsync(category, q, sort, ct: ct);
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

    // One page per configuration, the way rauvang.com has one page per
    // storage/region card. Colour and region are picked on the page; the
    // optional query values only choose which one it opens on, so every
    // choice stays linkable.
    [HttpGet("{slug}/{config}")]
    public async Task<IActionResult> Variant(string slug, string config, string? color = null, string? region = null, CancellationToken ct = default)
    {
        var configuration = await _catalog.GetConfigurationAsync(slug, config, ct);
        if (configuration is null)
            return NotFound();

        await LoadModelStripAsync(configuration.CategorySlug, configuration.ProductSlug, ct);
        return View(configuration);
    }

    // One owner for the model strip both the model and variant pages show.
    private async Task LoadModelStripAsync(string categorySlug, string activeSlug, CancellationToken ct)
    {
        var models = await _catalog.GetProductsAsync(categorySlug, ct: ct);
        ViewData[ModelStripViewModel.ViewDataKey] = new ModelStripViewModel(models, activeSlug);
    }
}
