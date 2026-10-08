using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Cart;
using AppleStore.Web.Models.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// The shopper's own orders (use cases 18-19). Another user's order id is a 404.
[Authorize]
[Route("Orders")]
public class OrdersController : Controller
{
    private readonly ICheckoutService _checkout;
    private readonly IOrderManagementService _orders;
    private readonly UserManager<User> _users;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ICheckoutService checkout, IOrderManagementService orders, UserManager<User> users, ILogger<OrdersController> logger)
    {
        _checkout = checkout;
        _orders = orders;
        _users = users;
        _logger = logger;
    }

    private int UserId => int.Parse(_users.GetUserId(User)!);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _orders.ListForUserAsync(UserId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var order = await _checkout.GetOrderAsync(UserId, id, ct);
        return order is null ? NotFound() : View(OrderPage.For(order, byStaff: false));
    }

    [HttpGet("{id:int}/Invoice")]
    public async Task<IActionResult> Invoice(int id, CancellationToken ct)
    {
        var order = await _checkout.GetOrderAsync(UserId, id, ct);
        return order is null ? NotFound() : View("Invoice", order);
    }

    [HttpPost("{id:int}/Cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        OrderChangeResult result;
        try
        {
            result = await _orders.CancelAsync(id, UserId, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Cancelling order {OrderId} failed", id);
            TempData[CartMessages.ErrorKey] = OrderMessages.SaveFailed;
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Outcome == OrderChangeOutcome.NotFound)
            return NotFound();
        var (text, good) = OrderMessages.ForCustomerCancel(result);
        TempData[good ? CartMessages.StatusKey : CartMessages.ErrorKey] = text;
        return RedirectToAction(nameof(Details), new { id });
    }
}
