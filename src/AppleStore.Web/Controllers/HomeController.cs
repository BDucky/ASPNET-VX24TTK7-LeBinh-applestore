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
        var carousel = await LoadAsync(HomeViewModel.CarouselSlugs, ct);
        var banners = await LoadAsync(HomeViewModel.BannerSlugs, ct);
        return View(new HomeViewModel(carousel, banners));
    }

    // Null-safe: a slug that moved or a deactivated product just drops out.
    private async Task<IReadOnlyList<ProductDetail>> LoadAsync(IEnumerable<string> slugs, CancellationToken ct)
    {
        var found = new List<ProductDetail>();
        foreach (var slug in slugs)
        {
            var product = await _catalog.GetBySlugAsync(slug, ct);
            if (product is not null)
                found.Add(product);
        }
        return found;
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
