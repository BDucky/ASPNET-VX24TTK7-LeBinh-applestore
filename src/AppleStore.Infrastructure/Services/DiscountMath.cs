using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// The one discount formula, for vouchers at checkout and for automatic
// promotions on prices: a percent rounds to whole dong, half away from zero,
// and no discount is larger than the amount it is taken from.
public static class DiscountMath
{
    public static decimal Amount(VoucherDiscountType type, decimal value, decimal basis)
    {
        var discount = type == VoucherDiscountType.Percent
            ? Math.Round(basis * value / 100m, 0, MidpointRounding.AwayFromZero)
            : value;
        return Math.Min(discount, basis);
    }
}
