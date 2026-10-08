using AppleStore.Infrastructure.Payments;
using AppleStore.Web.Models.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AppleStore.Web.Controllers;

// Stands in for the VNPay and MoMo pages while there are no sandbox
// credentials (decided 2026-10-08). It exists only when Payments:Mode is
// "Simulated", which the app refuses in Production. Pressing a button does
// what a real gateway does: it tells the shop's server first (as VNPay's IPN
// call would) and then sends the shopper back to /Payments/Return, both with
// a signed answer, so a repeat answer is exercised on every payment.
[AllowAnonymous]
[Route("PaymentSimulator")]
public class PaymentSimulatorController : Controller
{
    private static readonly string[] Results = [PaymentResultCode.Success, PaymentResultCode.Failed, PaymentResultCode.Cancelled];

    private readonly SimulatedPaymentGateway? _gateway;
    private readonly IPaymentService _payments;

    public PaymentSimulatorController(IPaymentService payments, SimulatedPaymentGateway gateway, IOptions<PaymentOptions> options)
    {
        _payments = payments;
        _gateway = options.Value.IsSimulated ? gateway : null;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        if (_gateway is null)
            return NotFound();

        var request = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        return _gateway.Verify(request) ? View(SimulatorPage.From(request)) : Invalid();
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(CancellationToken ct)
    {
        if (_gateway is null)
            return NotFound();

        var request = Request.Form
            .Where(f => f.Key is not ("result" or "__RequestVerificationToken"))
            .ToDictionary(f => f.Key, f => f.Value.ToString());
        var result = Request.Form["result"].ToString();
        if (!_gateway.Verify(request) || !Results.Contains(result))
            return Invalid();

        var answer = _gateway.Sign(new Dictionary<string, string>
        {
            ["paymentId"] = request["paymentId"],
            ["amount"] = request["amount"],
            ["result"] = result,
            ["txnId"] = "SIM-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
        });

        // The gateway's server call to the shop; the shopper's return below
        // delivers the same answer a second time.
        await _payments.HandleAsync(answer, ct);
        return Redirect("/Payments/Return?" + string.Join("&", answer.Select(a => $"{Uri.EscapeDataString(a.Key)}={Uri.EscapeDataString(a.Value)}")));
    }

    private ViewResult Invalid()
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("Invalid");
    }
}
