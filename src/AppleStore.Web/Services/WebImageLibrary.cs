using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Services;

// The shop's own product photos, the files in wwwroot/img/products. Admins
// pick from these instead of uploading (agreed 2026-10-08).
public sealed class WebImageLibrary : IImageLibrary
{
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];
    private readonly string _folder;

    public WebImageLibrary(IWebHostEnvironment environment)
    {
        _folder = Path.Combine(environment.WebRootPath, "img", "products");
    }

    public IReadOnlyList<string> All() =>
        Directory.Exists(_folder)
            ? Directory.EnumerateFiles(_folder)
                .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => "/img/products/" + Path.GetFileName(f))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
}
