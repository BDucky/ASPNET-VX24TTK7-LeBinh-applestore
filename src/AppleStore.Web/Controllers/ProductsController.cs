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

        return View(product);
    }
}
