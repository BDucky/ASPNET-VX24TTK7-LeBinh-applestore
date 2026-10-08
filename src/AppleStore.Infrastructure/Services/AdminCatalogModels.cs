namespace AppleStore.Infrastructure.Services;

// The photos the shop owns (wwwroot/img/products). Admins pick from these;
// nothing is uploaded (agreed 2026-10-08).
public interface IImageLibrary
{
    IReadOnlyList<string> All();
}

public sealed record ProductAdminRow(int Id, string Name, string Category, bool OnSale, int Variants, int Stock, string? ImageUrl);

// Version: the row's UpdatedAt in ticks, sent back with an edit so a change
// made meanwhile is noticed instead of overwritten.
public sealed record VariantEdit(int Id, string Sku, string? Configuration, string? Color, string? Region, decimal? Price, int StockQty, bool OnSale, long Version);

public sealed record ProductEdit(int Id, string Name, string Slug, string? Description, int CategoryId, decimal? BasePrice, bool OnSale,
    string? ImageUrl, long Version, IReadOnlyList<VariantEdit> Variants);

public sealed record ProductInput(string? Name, string? Description, int CategoryId, decimal? BasePrice, bool OnSale, string? ImageUrl);

public sealed record VariantInput(string? Sku, string? Configuration, string? Color, string? Region, decimal? Price, int StockQty, bool OnSale);

// SeenStock and Version: what the admin saw, so a sale or another edit
// meanwhile is not overwritten.
public sealed record VariantChange(decimal? Price, int StockQty, bool OnSale, int SeenStock, long Version);

public enum AdminCatalogOutcome
{
    Done,
    NotFound,
    MissingName,
    NameTaken,
    UnknownCategory,
    ImageNotInLibrary,
    InvalidPrice,
    InvalidStock,
    MissingSku,
    SkuTaken,
    // Someone else changed it after the admin opened the page.
    Changed,
}

public sealed record AdminCatalogResult(AdminCatalogOutcome Outcome, int? Id = null, int? CurrentStock = null);
