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

    public Task<IActionResult> Index(string? category, string? q, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
