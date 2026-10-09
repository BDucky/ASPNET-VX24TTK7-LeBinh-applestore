using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Admin;

// The words that differ between the vouchers and the promotions pages, the
// one place that switches on the kind for the web side. Which fields a kind
// has comes from VoucherKindRules, the same record the service reads.
public sealed record DiscountPageText(string Title, string Heading, string Noun, string Saved, string Deleted, string? Explainer, VoucherKindRules Rules)
{
    public static DiscountPageText For(VoucherKind kind) => kind switch
    {
        VoucherKind.Automatic => new("Promotions", "Promotion", "promotion", "Promotion saved.",
            "Promotion deleted. Orders placed during it keep the prices they were charged.",
            "While it runs, the products it covers show and charge the lower price, with the old one crossed out. When two cover a product, the larger discount wins; a voucher is then taken from the lower price.",
            VoucherKindRules.For(kind)),
        _ => new("Vouchers", "Voucher", "voucher", "Voucher saved.", "Voucher deleted. Orders that used it keep their code.", null, VoucherKindRules.For(kind)),
    };
}
