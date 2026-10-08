using System.Data.Common;
using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Payments;
using AppleStore.Web.Models.Cart;
using AppleStore.Web.Models.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Web.Controllers;

// Use case 17, online part: "Pay now" on an order, and the address the
// gateway sends the shopper back to. Every answer ends on a page that says
// what happened; a database failure says so too.
[Route("Payments")]
public class PaymentsController : Controller
{
    private readonly IPaymentService _payments;
    private readonly UserManager<User> _users;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService payments, UserManager<User> users, ILogger<PaymentsController> logger)
    {
        _payments = payments;
        _users = users;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("Pay/{orderId:int}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Pay(int orderId, CancellationToken ct) =>
        StartAsync(this, _payments, int.Parse(_users.GetUserId(User)!), orderId, _logger, ct);

    // Shared with checkout, which starts the payment right after placing.
    internal static async Task<IActionResult> StartAsync(Controller controller, IPaymentService payments, int userId, int orderId, ILogger logger, CancellationToken ct)
    {
        PayStart start;
        try
        {
            start = await payments.StartAsync(userId, orderId, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            logger.LogError(ex, "Starting a payment failed for order {OrderId}", orderId);
            controller.TempData[CartMessages.ErrorKey] = CheckoutMessages.Answer(CallbackOutcome.Rejected).Text;
            return controller.RedirectToAction("Details", "Orders", new { id = orderId });
        }

        if (start.Outcome == PayStartOutcome.NotFound)
            return controller.NotFound();
        if (start.Outcome == PayStartOutcome.Ready)
            return controller.Redirect(start.RedirectUrl!);

        controller.TempData[start.Outcome == PayStartOutcome.AlreadyPaid ? CartMessages.StatusKey : CartMessages.ErrorKey] = CheckoutMessages.Start(start.Outcome);
        return controller.RedirectToAction("Details", "Orders", new { id = orderId });
    }

    // Anyone may land here: the signature, not the cookie, says the answer is genuine.
    [AllowAnonymous]
    [HttpGet("Return")]
    public async Task<IActionResult> Return(CancellationToken ct)
    {
        var fields = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        CallbackResult result;
        try
        {
            result = await _payments.HandleAsync(fields, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Recording a payment answer failed");
            result = new CallbackResult(CallbackOutcome.Rejected);
        }

        var (text, good) = CheckoutMessages.Answer(result.Outcome);
        TempData[good ? CartMessages.StatusKey : CartMessages.ErrorKey] = text;
        return result.OrderId is { } id ? RedirectToAction("Details", "Orders", new { id }) : Redirect("/");
    }
}
