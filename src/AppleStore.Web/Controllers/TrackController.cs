using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// BM_ORDER_TRACK_01: anyone with the order number and the receiver's phone
// sees the status and tracking, never the address or the prices.
[AllowAnonymous]
[Route("Track")]
public class TrackController : Controller
{
    private readonly IOrderManagementService _orders;

    public TrackController(IOrderManagementService orders)
    {
        _orders = orders;
    }

    [HttpGet("")]
    public IActionResult Index() => View(new TrackPage(new TrackForm(), null, false));

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(TrackForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(new TrackPage(form, null, false));

        return View(new TrackPage(form, await _orders.TrackAsync(form.OrderId!.Value, form.Phone, ct), true));
    }
}
