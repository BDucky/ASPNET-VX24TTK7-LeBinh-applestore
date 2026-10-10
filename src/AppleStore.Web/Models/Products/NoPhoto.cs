namespace AppleStore.Web.Models.Products;

// The placeholder for a product with no licensed photo of its own model yet
// (owner's choice 2026-10-10: a placeholder, not an older model's photo).
// The one table from category to drawing; CssClass is an extra class such
// as "is-compact" for the model row.
public sealed record NoPhoto(string Kind, string? CssClass = null)
{
    public static NoPhoto For(string? categorySlug, string? cssClass = null) => new(categorySlug switch
    {
        "iphone" => "phone",
        "ipad" => "tablet",
        "mac" => "laptop",
        "watch" => "watch",
        "airpods" => "earbuds",
        "home" => "tv",
        _ => "accessory",
    }, cssClass);
}
