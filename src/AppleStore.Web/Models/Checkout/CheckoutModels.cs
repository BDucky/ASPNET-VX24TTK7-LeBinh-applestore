using System.ComponentModel.DataAnnotations;
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

    public string? Intent { get; set; }

    public bool IsPlace => string.Equals(Intent, "place", StringComparison.OrdinalIgnoreCase);
}

public sealed record CheckoutPage(CheckoutViewModel Form, CheckoutQuote Quote, string? VoucherMessage, bool VoucherApplied, string? Error);
