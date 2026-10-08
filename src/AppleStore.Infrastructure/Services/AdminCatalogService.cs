using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Admin edits to the catalog. Saves are conditional on what the admin saw
// (the row's UpdatedAt, and for a variant also its stock, which sales change
// without touching UpdatedAt), so neither another admin nor a sale made
// meanwhile is overwritten.
public class AdminCatalogService : IAdminCatalogService
{
    private readonly AppDbContext _db;
    private readonly IImageLibrary _images;
    private readonly TimeProvider _time;

    public AdminCatalogService(AppDbContext db, IImageLibrary images, TimeProvider time)
    {
        _db = db;
        _images = images;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ProductAdminRow>> ListAsync(string? search, CancellationToken ct = default)
    {
        var products = _db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            products = products.Where(p => EF.Functions.Like(p.Name, "%" + search.Trim() + "%"));

        return await products
            .OrderBy(p => p.Category.Name).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new ProductAdminRow(
                p.Id,
                p.Name,
                p.Category.Name,
                p.Status,
                _db.ProductVariants.Count(v => v.ProductId == p.Id),
                _db.ProductVariants.Where(v => v.ProductId == p.Id).Sum(v => v.StockQty),
                _db.ProductImages.Where(i => i.ProductId == p.Id).OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ProductEdit?> GetAsync(int productId, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
            return null;

        var image = await _db.ProductImages.Where(i => i.ProductId == productId).OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefaultAsync(ct);
        var variants = await _db.ProductVariants.AsNoTracking().Where(v => v.ProductId == productId).OrderBy(v => v.Id).ToListAsync(ct);
        var options = await VariantOptionLookup.LoadAsync(_db, variants.Select(v => v.Id).ToList(), ct);

        return new ProductEdit(product.Id, product.Name, product.Slug, product.Description, product.CategoryId, product.BasePrice, product.Status,
            image, product.UpdatedAt.Ticks,
            variants.Select(v => new VariantEdit(v.Id, v.SKU, options.Get(v.Id, "config"), options.Get(v.Id, "color"), options.Get(v.Id, "region"),
                v.Price, v.StockQty, v.Status, v.UpdatedAt.Ticks)).ToList());
    }

    public async Task<AdminCatalogResult> CreateAsync(ProductInput input, CancellationToken ct = default)
    {
        if (await CheckAsync(input, ct) is { } refused)
            return refused;

        var name = input.Name!.Trim();
        var slug = CatalogSlug.From(name);
        if (slug.Length == 0)
            return new AdminCatalogResult(AdminCatalogOutcome.MissingName);
        if (await _db.Products.AnyAsync(p => p.Slug == slug, ct))
            return new AdminCatalogResult(AdminCatalogOutcome.NameTaken);

        var sortOrder = await _db.Products.Where(p => p.CategoryId == input.CategoryId).Select(p => (int?)p.SortOrder).MaxAsync(ct) ?? 0;
        var product = new Product
        {
            Name = name,
            Slug = slug,
            Description = Blank(input.Description),
            CategoryId = input.CategoryId,
            BasePrice = input.BasePrice,
            Status = input.OnSale,
            SortOrder = sortOrder + 1,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        _db.Products.Add(product);
        if (Blank(input.ImageUrl) is { } image)
            _db.ProductImages.Add(new ProductImage { Product = product, ImageUrl = image, SortOrder = 0 });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another admin created the same name a moment earlier (unique slug index).
            _db.ChangeTracker.Clear();
            return new AdminCatalogResult(AdminCatalogOutcome.NameTaken);
        }
        return new AdminCatalogResult(AdminCatalogOutcome.Done, product.Id);
    }

    public async Task<AdminCatalogResult> UpdateAsync(int productId, ProductInput input, long version, CancellationToken ct = default)
    {
        if (await CheckAsync(input, ct) is { } refused)
            return refused;

        var next = RowVersion.Next(version, Now);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        // The slug is left alone so the product's page address and links keep working.
        var saved = await _db.Products
            .Where(p => p.Id == productId && p.UpdatedAt == new DateTime(version))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Name, input.Name!.Trim())
                .SetProperty(p => p.Description, Blank(input.Description))
                .SetProperty(p => p.CategoryId, input.CategoryId)
                .SetProperty(p => p.BasePrice, input.BasePrice)
                .SetProperty(p => p.Status, input.OnSale)
                .SetProperty(p => p.UpdatedAt, next), ct);
        if (saved != 1)
            return await _db.Products.AnyAsync(p => p.Id == productId, ct)
                ? new AdminCatalogResult(AdminCatalogOutcome.Changed)
                : new AdminCatalogResult(AdminCatalogOutcome.NotFound);

        // The admin manages the main photo; it replaces whatever was there.
        await _db.ProductImages.Where(i => i.ProductId == productId).ExecuteDeleteAsync(ct);
        if (Blank(input.ImageUrl) is { } image)
        {
            _db.ProductImages.Add(new ProductImage { ProductId = productId, ImageUrl = image, SortOrder = 0 });
            await _db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new AdminCatalogResult(AdminCatalogOutcome.Done, productId);
    }

    public async Task<AdminCatalogResult> SetOnSaleAsync(int productId, bool onSale, CancellationToken ct = default)
    {
        var now = Now;
        var changed = await _db.Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, onSale).SetProperty(p => p.UpdatedAt, now), ct);
        return new AdminCatalogResult(changed == 1 ? AdminCatalogOutcome.Done : AdminCatalogOutcome.NotFound, productId);
    }

    public async Task<AdminCatalogResult> AddVariantAsync(int productId, VariantInput input, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
            return new AdminCatalogResult(AdminCatalogOutcome.NotFound);
        var sku = input.Sku?.Trim();
        if (string.IsNullOrEmpty(sku))
            return new AdminCatalogResult(AdminCatalogOutcome.MissingSku);
        if (CheckVariant(input.Price, input.StockQty) is { } refused)
            return refused;
        var upper = sku.ToUpperInvariant();
        if (await _db.ProductVariants.AnyAsync(v => v.SKU.ToUpper() == upper, ct))
            return new AdminCatalogResult(AdminCatalogOutcome.SkuTaken);

        var variant = new ProductVariant
        {
            ProductId = productId,
            SKU = sku,
            Price = input.Price,
            StockQty = input.StockQty,
            Status = input.OnSale,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        _db.ProductVariants.Add(variant);
        foreach (var (code, value) in new[] { ("config", input.Configuration), ("color", input.Color), ("region", input.Region) })
        {
            if (Blank(value) is { } text)
                await TagAsync(variant, code, text, ct);
        }
        await _db.SaveChangesAsync(ct);
        return new AdminCatalogResult(AdminCatalogOutcome.Done, variant.Id);
    }

    public async Task<AdminCatalogResult> UpdateVariantAsync(int variantId, VariantChange change, CancellationToken ct = default)
    {
        if (CheckVariant(change.Price, change.StockQty) is { } refused)
            return refused;

        var next = RowVersion.Next(change.Version, Now);
        var saved = await _db.ProductVariants
            .Where(v => v.Id == variantId && v.UpdatedAt == new DateTime(change.Version) && v.StockQty == change.SeenStock)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Price, change.Price)
                .SetProperty(v => v.StockQty, change.StockQty)
                .SetProperty(v => v.Status, change.OnSale)
                .SetProperty(v => v.UpdatedAt, next), ct);
        if (saved == 1)
            return new AdminCatalogResult(AdminCatalogOutcome.Done, variantId);

        var current = await _db.ProductVariants.Where(v => v.Id == variantId).Select(v => (int?)v.StockQty).FirstOrDefaultAsync(ct);
        return current is null
            ? new AdminCatalogResult(AdminCatalogOutcome.NotFound)
            : new AdminCatalogResult(AdminCatalogOutcome.Changed, CurrentStock: current);
    }

    private async Task<AdminCatalogResult?> CheckAsync(ProductInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return new AdminCatalogResult(AdminCatalogOutcome.MissingName);
        if (!await _db.Categories.AnyAsync(c => c.Id == input.CategoryId, ct))
            return new AdminCatalogResult(AdminCatalogOutcome.UnknownCategory);
        // Only the shop's own photos, matched exactly: no outside address, no path tricks.
        if (Blank(input.ImageUrl) is { } image && !_images.All().Contains(image))
            return new AdminCatalogResult(AdminCatalogOutcome.ImageNotInLibrary);
        if (input.BasePrice is <= 0)
            return new AdminCatalogResult(AdminCatalogOutcome.InvalidPrice);
        return null;
    }

    // Same rules as the schema: a price is positive or not set ("Contact for
    // price"), stock is never negative.
    private static AdminCatalogResult? CheckVariant(decimal? price, int stock) =>
        price is <= 0 ? new AdminCatalogResult(AdminCatalogOutcome.InvalidPrice)
        : stock < 0 ? new AdminCatalogResult(AdminCatalogOutcome.InvalidStock)
        : null;

    private async Task TagAsync(ProductVariant variant, string code, string value, CancellationToken ct)
    {
        var type = await _db.OptionTypes.FirstOrDefaultAsync(t => t.Code == code, ct)
            ?? _db.OptionTypes.Local.FirstOrDefault(t => t.Code == code)
            ?? _db.OptionTypes.Add(new OptionType { Code = code }).Entity;
        var option = (type.Id == 0 ? null : await _db.OptionValues.FirstOrDefaultAsync(v => v.OptionTypeId == type.Id && v.Value == value, ct))
            ?? _db.OptionValues.Add(new OptionValue { OptionType = type, Value = value }).Entity;
        _db.VariantOptions.Add(new VariantOption { Variant = variant, OptionType = type, OptionValue = option });
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
