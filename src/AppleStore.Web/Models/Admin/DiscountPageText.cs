using AppleStore.Domain.Enums;

namespace AppleStore.Web.Models.Admin;

// The words that differ between the vouchers and the promotions pages, the
// one place that switches on the kind for the web side.
public sealed record DiscountPageText(string Title, string Noun, string Saved, string Deleted)
{
    public static DiscountPageText For(VoucherKind kind) => kind switch
    {
        VoucherKind.Automatic => new("Promotions", "promotion", "Promotion saved.",
            "Promotion deleted. Orders placed during it keep the prices they were charged."),
        _ => new("Vouchers", "voucher", "Voucher saved.", "Voucher deleted. Orders that used it keep their code."),
    };
}
