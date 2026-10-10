using AppleStore.Infrastructure.Formatting;

namespace AppleStore.Tests;

// How staff pages name a variant. A configuration name already holds the
// product's name ("iPhone 18 Pro Max 256GB ( VN )"), so the product name is
// used only when there is no configuration (found in the report's
// screenshots 2026-10-10: "iPhone 18 Pro Max iPhone 18 Pro Max 256GB").
public class VariantTextTests
{
    [Theory]
    [InlineData("iPhone 18 Pro Max", "iPhone 18 Pro Max 256GB ( VN )", "Black", "Active Online", "iPhone 18 Pro Max 256GB ( VN ) Black Active Online")]
    [InlineData("AirPods 4", null, null, null, "AirPods 4")]
    [InlineData("AirPods 4", null, "White", null, "AirPods 4 White")]
    public void A_variant_is_named_once(string product, string? configuration, string? color, string? region, string expected)
    {
        Assert.Equal(expected, VariantText.Label(product, configuration, color, region));
    }
}
