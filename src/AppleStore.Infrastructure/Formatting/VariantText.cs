namespace AppleStore.Infrastructure.Formatting;

// A variant's options in one line ("256GB Blue VN/A"), the one way staff
// pages name a variant next to its product.
public static class VariantText
{
    public static string Options(string? configuration, string? color, string? region) =>
        string.Join(" ", new[] { configuration, color, region }.OfType<string>());

    public static string Label(string productName, string? configuration, string? color, string? region) => throw new NotImplementedException();
}
