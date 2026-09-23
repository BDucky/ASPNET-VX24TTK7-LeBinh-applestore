using System.Diagnostics;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using AppleStore.Web.Models;

namespace AppleStore.Web.Controllers;

public class HomeController : Controller
{
    private readonly IProductCatalogService _catalog;

    public HomeController(IProductCatalogService catalog)
    {
        _catalog = catalog;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // The newest, most-asked-about product gets a homepage spotlight, not
        // buried in the grid. Null-safe: if the slug ever moves or the product
        // is deactivated, the section just doesn't render, nothing breaks.
        var featured = await _catalog.GetBySlugAsync("iphone-18-pro", ct);
        return View(featured);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Credits()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
