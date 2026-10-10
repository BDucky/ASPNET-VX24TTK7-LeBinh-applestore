namespace AppleStore.Infrastructure.Formatting;

// How staff pages name a variant, in one line. A configuration name already
// holds the product's name ("iPhone 18 Pro Max 256GB ( VN )"), so the
// product name stands in only when there is no configuration.
public static class VariantText
{
    public static string Label(string productName, string? configuration, string? color, string? region) =>
        string.Join(" ", new[] { configuration ?? productName, color, region }.OfType<string>());
}
