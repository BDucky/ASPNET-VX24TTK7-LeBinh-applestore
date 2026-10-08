using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models.Cart;
using AppleStore.Web.Models.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Use cases 22-24 for staff (Admin or Employee): list, confirm, ship with a
// tracking number, complete, cancel. Which buttons exist comes from
// OrderTransitions; every press ends back on the order with a message.
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Employee)}")]
[Route("Admin/Orders")]
public class OrdersController : Controller
{
    private readonly IOrderManagementService _orders;
    private readonly ICheckoutService _checkout;
    private readonly IOrderNotifier _notifier;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderManagementService orders, ICheckoutService checkout, IOrderNotifier notifier, ILogger<OrdersController> logger)
    {
        _orders = orders;
        _checkout = checkout;
        _notifier = notifier;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(OrderStatus? status, CancellationToken ct) =>
        View(new StaffOrderList(await _orders.ListAsync(status, ct), status));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var order = await _checkout.GetOrderForStaffAsync(id, ct);
        return order is null ? NotFound() : View(OrderPage.For(order, byStaff: true));
    }

    [HttpGet("{id:int}/Invoice")]
    public async Task<IActionResult> Invoice(int id, CancellationToken ct)
    {
        var order = await _checkout.GetOrderForStaffAsync(id, ct);
        return order is null ? NotFound() : View("Invoice", order);
    }

    [HttpPost("{id:int}/Confirm"), ValidateAntiForgeryToken]
    public Task<IActionResult> Confirm(int id, CancellationToken ct) =>
        ChangeAsync(id, OrderAction.Confirm, () => _orders.ConfirmAsync(id, ct), ct);

    [HttpPost("{id:int}/Ship"), ValidateAntiForgeryToken]
    public Task<IActionResult> Ship(int id, string? carrier, string? trackingNo, CancellationToken ct) =>
        ChangeAsync(id, OrderAction.Ship, () => _orders.ShipAsync(id, new ShipmentInput(carrier, trackingNo), ct), ct);

    [HttpPost("{id:int}/Complete"), ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(int id, CancellationToken ct) =>
        ChangeAsync(id, OrderAction.Complete, () => _orders.CompleteAsync(id, ct), ct);

    [HttpPost("{id:int}/Cancel"), ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        ChangeAsync(id, OrderAction.Cancel, () => _orders.CancelAsync(id, null, ct), ct);

    private async Task<IActionResult> ChangeAsync(int id, OrderAction action, Func<Task<OrderChangeResult>> change, CancellationToken ct)
    {
        OrderChangeResult result;
        try
        {
            result = await change();
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "{Action} on order {OrderId} failed", action, id);
            TempData[CartMessages.ErrorKey] = OrderMessages.SaveFailed;
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Outcome == OrderChangeOutcome.NotFound)
            return NotFound();
        if (action == OrderAction.Ship && result.Outcome == OrderChangeOutcome.Done)
            await _notifier.OrderShippedAsync(id, Url.Action("Index", "Track", new { area = "" }, Request.Scheme)!, ct);

        var (text, good) = OrderMessages.ForStaff(action, result);
        TempData[good ? CartMessages.StatusKey : CartMessages.ErrorKey] = text;
        return RedirectToAction(nameof(Details), new { id });
    }
}
