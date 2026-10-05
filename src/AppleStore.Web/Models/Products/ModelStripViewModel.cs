using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Products;

// The row of model tiles rauvang.com repeats at the top of its category,
// model, and variant pages: every model in the current category, with the
// one being viewed marked active.
public record ModelStripViewModel(IReadOnlyList<ProductSummary> Models, string? ActiveSlug)
{
    public const string ViewDataKey = "ModelStrip";
}
