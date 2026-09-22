using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Products;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductCatalogService _catalog;

    public ProductsController(IProductCatalogService catalog)
    {
        _catalog = catalog;
    }

    public async Task<IActionResult> Index(string? category, string? q, CancellationToken ct)
    {
        var products = await _catalog.GetProductsAsync(category, q, ct);
        return View(new ProductListViewModel(products, category, q));
    }

    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var product = await _catalog.GetBySlugAsync(slug, ct);
        if (product is null)
            return NotFound();

        return View(product);
    }
}
