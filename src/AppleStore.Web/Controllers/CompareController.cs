using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models;
using AppleStore.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// Use case 10, open to visitors. The list is a cookie (CompareCookie); every
// outcome comes back as a message on the next page, like the cart.
[Route("Compare")]
public class CompareController : Controller
{
    private readonly ICompareService _compare;
    private readonly ILogger<CompareController> _logger;

    public CompareController(ICompareService compare, ILogger<CompareController> logger)
    {
        _compare = compare;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var ids = CompareCookie.Read(Request);
        IReadOnlyList<CompareColumn> columns;
        try
        {
            columns = await _compare.BuildAsync(ids, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            // The cookie is left alone: the list is still good, only the read failed.
            _logger.LogError(ex, "Compare page failed for {Count} products", ids.Count);
            ViewData["CompareLoadFailed"] = true;
            return View(Array.Empty<CompareColumn>());
        }
        // Drop what the page left out, so the count in the nav matches.
        if (!columns.Select(c => c.ProductId).SequenceEqual(ids))
            CompareCookie.Write(HttpContext, columns.Select(c => c.ProductId).ToList());
        return View(columns);
    }

    [HttpPost("Add"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, string? returnUrl, CancellationToken ct)
    {
        var back = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!;
        try
        {
            var result = await _compare.AddAsync(CompareCookie.Read(Request), productId, ct);
            CompareCookie.Write(HttpContext, result.Ids);
            if (CompareMessages.Error(result.Outcome) is { } error)
                TempData[CompareMessages.ErrorKey] = error;
            else
                TempData[CompareMessages.StatusKey] = CompareMessages.Status(result.Outcome);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Compare add failed for product {ProductId}", productId);
            TempData[CompareMessages.ErrorKey] = CompareMessages.Failed;
        }
        return Redirect(back);
    }

    // Removing and clearing touch only the cookie, so they cannot fail on the database.
    [HttpPost("Remove"), ValidateAntiForgeryToken]
    public IActionResult Remove(int productId)
    {
        CompareCookie.Write(HttpContext, CompareCookie.Read(Request).Where(id => id != productId).ToList());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Clear"), ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        CompareCookie.Write(HttpContext, []);
        return RedirectToAction(nameof(Index));
    }
}
