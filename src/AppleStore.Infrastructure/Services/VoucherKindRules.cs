using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// What each kind of Vouchers row has (use case 28): a code voucher has a code,
// a minimum order and a usage limit; a promotion has a name instead. The one
// place that says so, read by AdminVoucherService and the admin pages.
public sealed record VoucherKindRules(bool HasCode, bool HasName, bool HasLimits)
{
    public static VoucherKindRules For(VoucherKind kind) => kind switch
    {
        VoucherKind.Automatic => new(HasCode: false, HasName: true, HasLimits: false),
        _ => new(HasCode: true, HasName: false, HasLimits: true),
    };

    // What a row is called in lists and headings.
    public string? Label(string? code, string? name) => HasCode ? code : name;
}
