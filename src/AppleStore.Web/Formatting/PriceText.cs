using System.Globalization;

namespace AppleStore.Web.Formatting;

// Prices print the way rauvang.com prints them on its cards: whole dong,
// dots between thousands, "VNĐ" after. A missing price is never shown as 0.
public static class PriceText
{
    private static readonly NumberFormatInfo DotThousands = new() { NumberGroupSeparator = ".", NumberGroupSizes = new[] { 3 } };

    public static string Vnd(decimal? price) =>
        price is null
            ? "Contact for price"
            : Math.Round(price.Value, MidpointRounding.AwayFromZero).ToString("#,0", DotThousands) + " VNĐ";
}
