using System.ComponentModel.DataAnnotations;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Account;

namespace AppleStore.Web.Models.Checkout;

// The checkout form: the delivery fields shared with saved addresses, plus
// the note, the voucher and the total the shopper saw. Intent is "apply"
// (price again, no write) or "place".
public class CheckoutViewModel : DeliveryFields
{
    [StringLength(400), Display(Name = "Note for the delivery (optional)")]
    public string? Note { get; set; }

    [StringLength(40), Display(Name = "Voucher code")]
    public string? VoucherCode { get; set; }

    public decimal ExpectedTotal { get; set; }

    [Display(Name = "Payment")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cod;

    public string? Intent { get; set; }

    public bool IsPlace => string.Equals(Intent, "place", StringComparison.OrdinalIgnoreCase);
}

public sealed record CheckoutPage(CheckoutViewModel Form, CheckoutQuote Quote, string? VoucherMessage, bool VoucherApplied, string? Error, bool OnlineAvailable);

// What the simulator page shows: the signed request, read back.
public sealed record SimulatorPage(IReadOnlyDictionary<string, string> Fields, string OrderId, decimal Amount, string GatewayName)
{
    public static SimulatorPage From(IReadOnlyDictionary<string, string> fields) => new(
        fields,
        fields.GetValueOrDefault("orderId", ""),
        decimal.TryParse(fields.GetValueOrDefault("amount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) ? amount : 0m,
        fields.GetValueOrDefault("method") == nameof(PaymentMethod.MoMo) ? "MoMo" : "VNPay");
}
