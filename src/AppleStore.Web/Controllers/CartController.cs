using System.Data.Common;
using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Web.Controllers;

// The signed-in user's cart (use cases 11-14). A visitor is sent to sign in
// by [Authorize]; the product page shows them a sign-in link instead of the
// form. Every outcome comes back as a message on the next page (TempData),
// so no post ends on an error page or a blank one.
[Authorize]
[Route("Cart")]
public class CartController : Controller
{
    private readonly ICartService _cart;
    private readonly UserManager<User> _users;
    private readonly ILogger<CartController> _logger;

    public CartController(ICartService cart, UserManager<User> users, ILogger<CartController> logger)
    {
        _cart = cart;
        _users = users;
        _logger = logger;
    }

    private int UserId => int.Parse(_users.GetUserId(User)!);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _cart.GetAsync(UserId, ct));

    // Signing in from a challenged POST comes back here as a GET.
    [HttpGet("Add")]
    public IActionResult Add() => RedirectToAction(nameof(Index));

    [HttpPost("Add"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(CartAddForm form, CancellationToken ct)
    {
        var back = Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl! : Url.Action(nameof(Index))!;
        var result = await RunAsync(() => _cart.AddAsync(UserId, form.VariantId, form.Quantity ?? 0, ct), "add", form.VariantId);
        if (result is null)
            return Redirect(back);
        if (result.Outcome != CartOutcome.Ok)
        {
            TempData[CartMessages.ErrorKey] = CartMessages.ForAdd(result);
            return Redirect(back);
        }

        TempData[CartMessages.StatusKey] = CartMessages.Added;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Update"), ValidateAntiForgeryToken]
    public Task<IActionResult> Update(CartLineForm form, CancellationToken ct) =>
        ChangeAsync(() => _cart.SetQuantityAsync(UserId, form.ItemId, form.Quantity ?? 0, ct), CartMessages.Updated, "update", form.ItemId);

    [HttpPost("Remove"), ValidateAntiForgeryToken]
    public Task<IActionResult> Remove(CartLineForm form, CancellationToken ct) =>
        ChangeAsync(() => _cart.RemoveAsync(UserId, form.ItemId, ct), CartMessages.Removed, "remove", form.ItemId);

    private async Task<IActionResult> ChangeAsync(Func<Task<CartResult>> change, string done, string action, int itemId)
    {
        var result = await RunAsync(change, action, itemId);
        if (result is not null)
        {
            if (result.Outcome == CartOutcome.Ok)
                TempData[CartMessages.StatusKey] = done;
            else
                TempData[CartMessages.ErrorKey] = CartMessages.ForChange(result);
        }
        return RedirectToAction(nameof(Index));
    }

    // Null when the database failed; the message for that is already set.
    private async Task<CartResult?> RunAsync(Func<Task<CartResult>> call, string action, int id)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Cart {Action} failed for user {UserId}, id {Id}", action, UserId, id);
            TempData[CartMessages.ErrorKey] = CartMessages.Failed;
            return null;
        }
    }
}
