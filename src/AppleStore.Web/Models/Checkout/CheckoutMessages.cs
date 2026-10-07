using AppleStore.Domain.Enums;
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

    public static string Payment(OrderPaymentStatus status) => status == OrderPaymentStatus.Paid ? "Paid" : "Not paid yet";
}
