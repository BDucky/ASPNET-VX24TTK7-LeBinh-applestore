using System.Data.Common;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Payments;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Cart;
using AppleStore.Web.Models.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Web.Controllers;

// Use cases 15-16. One form with two buttons: "Apply" prices the order again
// with the voucher and writes nothing; "Place order" validates the delivery
// details and places it. Whatever goes wrong, the shopper lands on a page
// that says what happened and what to do next.
[Authorize]
[Route("Checkout")]
public class CheckoutController : Controller
{
    private readonly ICheckoutService _checkout;
    private readonly IPaymentService _payments;
    private readonly IProfileService _profile;
    private readonly IOrderNotifier _notifier;
    private readonly UserManager<User> _users;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(ICheckoutService checkout, IPaymentService payments, IProfileService profile, IOrderNotifier notifier,
        UserManager<User> users, ILogger<CheckoutController> logger)
    {
        _notifier = notifier;
        _checkout = checkout;
        _payments = payments;
        _profile = profile;
        _users = users;
        _logger = logger;
    }

    private int UserId => int.Parse(_users.GetUserId(User)!);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var quote = await _checkout.QuoteAsync(UserId, null, ct);
        if (BackToCart(quote) is { } back)
            return back;

        var form = await StartingFormAsync(ct);
        form.ExpectedTotal = quote.Total;
        return View(new CheckoutPage(form, quote, null, false, null, _payments.OnlineAvailable));
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel form, CancellationToken ct)
    {
        var quote = await _checkout.QuoteAsync(UserId, form.VoucherCode, ct);
        if (BackToCart(quote) is { } back)
            return back;

        if (!form.IsPlace)
        {
            // Only pricing again: delivery details are checked when placing.
            ModelState.Clear();
            return Render(form, quote, null);
        }
        if (form.PaymentMethod != PaymentMethod.Cod && !_payments.OnlineAvailable)
            ModelState.AddModelError(nameof(CheckoutViewModel.PaymentMethod), CheckoutMessages.MethodUnavailable);
        if (!ModelState.IsValid)
            return Render(form, quote, null);

        PlaceOrderResult result;
        try
        {
            var delivery = new DeliveryInput(form.FullName, form.Phone, form.AddressLine, form.Ward, form.District, form.City, form.Note);
            result = await _checkout.PlaceOrderAsync(UserId, delivery, form.VoucherCode, form.ExpectedTotal, form.PaymentMethod, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Placing an order failed for user {UserId}", UserId);
            TempData[CartMessages.ErrorKey] = CheckoutMessages.Failed;
            return RedirectToAction(nameof(Index));
        }

        switch (result.Outcome)
        {
            case PlaceOrderOutcome.Placed:
                TempData[CartMessages.StatusKey] = CheckoutMessages.Placed;
                await _notifier.OrderPlacedAsync(result.OrderId!.Value,
                    Url.Action("Details", "Orders", new { id = result.OrderId }, Request.Scheme)!, ct);
                // Online: straight on to the gateway; the order page offers
                // "Pay now" if that does not work out.
                if (form.PaymentMethod != PaymentMethod.Cod)
                    return await PaymentsController.StartAsync(this, _payments, UserId, result.OrderId!.Value, _logger, ct);
                return RedirectToAction("Details", "Orders", new { id = result.OrderId });
            case PlaceOrderOutcome.CartEmpty:
                return ToCart(CheckoutMessages.CartEmpty);
            case PlaceOrderOutcome.CartHasProblems:
                return ToCart(CheckoutMessages.CartHasProblems);
            case PlaceOrderOutcome.CartChanged:
                return ToCart(CheckoutMessages.CartChanged);
            case PlaceOrderOutcome.OutOfStock:
                return ToCart(CheckoutMessages.OutOfStock(result.ProductName));
            case PlaceOrderOutcome.VoucherRefused:
                return Render(form, await _checkout.QuoteAsync(UserId, form.VoucherCode, ct), CheckoutMessages.CheckVoucher);
            default:
                return Render(form, await _checkout.QuoteAsync(UserId, form.VoucherCode, ct), CheckoutMessages.TotalChanged);
        }
    }

    // The page always shows the total it will hold the shopper to.
    private ViewResult Render(CheckoutViewModel form, CheckoutQuote quote, string? error)
    {
        form.ExpectedTotal = quote.Total;
        var voucherMessage = quote.VoucherCode is null ? null : CheckoutMessages.Voucher(quote);
        return View(nameof(Index), new CheckoutPage(form, quote, voucherMessage, quote.VoucherCode is not null && quote.VoucherProblem == VoucherProblem.None, error,
            _payments.OnlineAvailable));
    }

    private IActionResult? BackToCart(CheckoutQuote quote) => quote.Problem switch
    {
        CheckoutProblem.CartEmpty => ToCart(CheckoutMessages.CartEmpty),
        CheckoutProblem.CartHasProblems => ToCart(CheckoutMessages.CartHasProblems),
        _ => null,
    };

    private RedirectToActionResult ToCart(string message)
    {
        TempData[CartMessages.ErrorKey] = message;
        return RedirectToAction("Index", "Cart");
    }

    // The default saved address, else the first, else the profile's name and phone.
    private async Task<CheckoutViewModel> StartingFormAsync(CancellationToken ct)
    {
        var addresses = await _profile.GetAddressesAsync(UserId, ct);
        var address = addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault();
        if (address is not null)
        {
            return new CheckoutViewModel
            {
                FullName = address.FullName,
                Phone = address.Phone,
                AddressLine = address.AddressLine,
                Ward = address.Ward,
                District = address.District,
                City = address.City,
            };
        }

        var user = await _users.GetUserAsync(User);
        return new CheckoutViewModel { FullName = user?.FullName ?? "", Phone = user?.Phone ?? "" };
    }
}
