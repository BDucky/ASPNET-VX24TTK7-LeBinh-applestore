using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Payments;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Formatting;

namespace AppleStore.Web.Models.Checkout;

// The one place checkout results become sentences.
public static class CheckoutMessages
{
    public const string CartEmpty = "Your cart is empty.";
    public const string CartHasProblems = "Remove or change the lines marked in red, then check out.";
    public const string CartChanged = "Your cart changed in another tab, so nothing was ordered. Check it and try again.";
    public const string TotalChanged = "The total changed since you opened this page. Check it and place the order again.";
    public const string CheckVoucher = "Check the voucher, then place the order again.";
    public const string Placed = "Thank you. Your order was placed.";
    public const string Failed = "We could not place your order, and nothing was charged. Please try again.";

    public const string MethodUnavailable = "This payment method is not available.";

    public static string OutOfStock(string? name) => $"{name ?? "An item"} no longer has enough stock, so nothing was ordered. Check your cart.";

    public static string Voucher(CheckoutQuote quote) => quote.VoucherProblem switch
    {
        VoucherProblem.None => $"Voucher {quote.VoucherCode} applied.",
        VoucherProblem.NotFound => "There is no voucher with that code.",
        VoucherProblem.Inactive => "This voucher is no longer active.",
        VoucherProblem.NotStarted => "This voucher is not valid yet.",
        VoucherProblem.Expired => "This voucher has expired.",
        VoucherProblem.UsedUp => "This voucher has been fully used.",
        VoucherProblem.BelowMinimum => $"This voucher needs an order of at least {PriceText.Vnd(quote.MinOrderAmount)}.",
        VoucherProblem.NoEligibleProducts => "This voucher does not apply to anything in your cart.",
        _ => "This voucher cannot be used.",
    };

    public static string Status(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Waiting for confirmation",
        OrderStatus.Confirmed => "Confirmed",
        OrderStatus.Shipping => "On the way",
        OrderStatus.Completed => "Delivered",
        OrderStatus.Cancelled => "Cancelled",
        _ => status.ToString(),
    };

    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.Cod => "Cash on delivery",
        PaymentMethod.VnPay => "VNPay",
        PaymentMethod.MoMo => "MoMo",
        _ => method.ToString(),
    };

    // After the gateway's answer: (message, is it good news).
    public static (string Text, bool Good) Answer(CallbackOutcome outcome) => outcome switch
    {
        CallbackOutcome.Paid => ("Payment received. Thank you.", true),
        // The gateway's server call usually lands first, so the shopper's
        // return finds the payment already recorded: same good news.
        CallbackOutcome.AlreadyPaid => ("Payment received. Thank you.", true),
        CallbackOutcome.Failed => ("The payment did not go through, and nothing was charged. You can try again.", false),
        CallbackOutcome.Cancelled => ("The payment was cancelled. You can try again.", false),
        CallbackOutcome.PaidTwice => ("This order was already paid, so this second payment will be refunded. Contact us if it does not arrive.", false),
        CallbackOutcome.PaidAfterCancel => ("This order was cancelled, so this payment will be refunded. Contact us if it does not arrive.", false),
        _ => ("We could not confirm this payment. If money was taken, contact us with your order number.", false),
    };

    public static string? Start(PayStartOutcome outcome) => outcome switch
    {
        PayStartOutcome.AlreadyPaid => "This order is already paid.",
        PayStartOutcome.NotOnline => "This order is paid on delivery.",
        PayStartOutcome.Unavailable => "Online payment is not available right now. Please try again later.",
        PayStartOutcome.Cancelled => "This order was cancelled, so it cannot be paid.",
        _ => null,
    };

    public static string Payment(OrderPaymentStatus status) => status == OrderPaymentStatus.Paid ? "Paid" : "Not paid yet";
}
