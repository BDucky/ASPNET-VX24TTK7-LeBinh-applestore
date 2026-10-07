using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// The shopper's own orders. Another user's order id is a 404. The list and
// tracking come with task 8.
[Authorize]
[Route("Orders")]
public class OrdersController : Controller
{
    private readonly ICheckoutService _checkout;
    private readonly UserManager<User> _users;

    public OrdersController(ICheckoutService checkout, UserManager<User> users)
    {
        _checkout = checkout;
        _users = users;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var order = await _checkout.GetOrderAsync(int.Parse(_users.GetUserId(User)!), id, ct);
        return order is null ? NotFound() : View(order);
    }
}
