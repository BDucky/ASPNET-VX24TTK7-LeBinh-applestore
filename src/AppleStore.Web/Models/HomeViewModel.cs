using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models;

// rauvang.com's homepage is a rotating top banner followed by a stack of
// full-width banners, one per product line, each linking to that model's
// page. The slug lists below are this app's own curation, not rauvang's.
public record HomeViewModel(IReadOnlyList<ProductDetail> Carousel, IReadOnlyList<ProductDetail> Banners)
{
    public static readonly IReadOnlyList<string> CarouselSlugs = new[] { "iphone-18-pro", "iphone-18-pro-max", "iphone-air" };

    public static readonly IReadOnlyList<string> BannerSlugs = new[] { "apple-watch-series-12", "macbook-neo", "ipad-mini", "apple-watch-ultra-4", "airpods-5" };
}
