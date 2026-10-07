using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.ViewComponents;

// The cart link in the nav, with the number of items for a signed-in user.
// A count that cannot be read is left out rather than breaking every page.
public class CartCountViewComponent : ViewComponent
{
    private readonly ICartService _cart;
    private readonly UserManager<User> _users;
    private readonly ILogger<CartCountViewComponent> _logger;

    public CartCountViewComponent(ICartService cart, UserManager<User> users, ILogger<CartCountViewComponent> logger)
    {
        _cart = cart;
        _users = users;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        int? count = null;
        if (_users.GetUserId(UserClaimsPrincipal) is { } id)
        {
            try
            {
                count = await _cart.CountAsync(int.Parse(id), HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Cart count could not be read for user {UserId}", id);
            }
        }
        return View(count);
    }
}
