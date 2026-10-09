using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// The signed-in user's cart, one row in Carts per user and one CartItems row
// per variant (both unique indexes). The cart holds no stock and no price:
// prices are read live, and checkout checks stock again.
public class CartService : ICartService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public CartService(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<CartResult> AddAsync(int userId, int variantId, int quantity, CancellationToken ct = default)
    {
        if (quantity < 1)
            return new CartResult(CartOutcome.InvalidQuantity);

        var variant = await _db.ProductVariants
            .Where(v => v.Id == variantId)
            .Select(v => new { v.ProductId, OnSale = v.Status && v.Product.Status, v.Price, v.StockQty })
            .FirstOrDefaultAsync(ct);
        if (variant is null)
            return new CartResult(CartOutcome.NotFound);
        if (Refusal(variant.OnSale, variant.Price, variant.StockQty) is { } refused)
            return new CartResult(refused);

        // A unique-index violation means another request created this cart or
        // line between the read and the save. On the second attempt both exist,
        // so it takes the update path, which cannot violate either index.
        try
        {
            return await AddOnceAsync(userId, variant.ProductId, variantId, variant.StockQty, quantity, ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return await AddOnceAsync(userId, variant.ProductId, variantId, variant.StockQty, quantity, ct);
        }
    }

    private async Task<CartResult> AddOnceAsync(int userId, int productId, int variantId, int stock, int quantity, CancellationToken ct)
    {
        var cartId = await _db.Carts.Where(c => c.UserId == userId).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (cartId is { } id)
        {
            // The increment and the stock check are one statement, so two requests
            // cannot both pass the check against the same old quantity.
            var raised = await _db.CartItems
                .Where(i => i.CartId == id && i.VariantId == variantId && i.Quantity + quantity <= stock)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity + quantity), ct);
            if (raised == 1)
                return CartResult.Ok;

            var held = await _db.CartItems
                .Where(i => i.CartId == id && i.VariantId == variantId)
                .Select(i => (int?)i.Quantity)
                .FirstOrDefaultAsync(ct);
            if (held is { } inCart)
                return new CartResult(CartOutcome.ExceedsStock, Math.Max(0, stock - inCart));
        }

        if (quantity > stock)
            return new CartResult(CartOutcome.ExceedsStock, stock);

        var item = new CartItem { ProductId = productId, VariantId = variantId, Quantity = quantity };
        if (cartId is { } existing)
            item.CartId = existing;
        else
            item.Cart = new Cart { UserId = userId };
        _db.CartItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return CartResult.Ok;
    }

    public async Task<CartResult> SetQuantityAsync(int userId, int itemId, int quantity, CancellationToken ct = default)
    {
        if (quantity < 1)
            return new CartResult(CartOutcome.InvalidQuantity);

        var line = await OwnLine(userId, itemId)
            .Select(i => new { OnSale = i.Variant.Status && i.Variant.Product.Status, i.Variant.Price, i.Variant.StockQty })
            .FirstOrDefaultAsync(ct);
        if (line is null)
            return new CartResult(CartOutcome.NotFound);
        if (Refusal(line.OnSale, line.Price, line.StockQty) is { } refused)
            return new CartResult(refused);
        if (quantity > line.StockQty)
            return new CartResult(CartOutcome.ExceedsStock, line.StockQty);

        var changed = await OwnLine(userId, itemId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, quantity), ct);
        return changed == 1 ? CartResult.Ok : new CartResult(CartOutcome.NotFound);
    }

    public async Task<CartResult> RemoveAsync(int userId, int itemId, CancellationToken ct = default)
    {
        var removed = await OwnLine(userId, itemId).ExecuteDeleteAsync(ct);
        return removed == 1 ? CartResult.Ok : new CartResult(CartOutcome.NotFound);
    }

    public async Task<CartView> GetAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _db.CartItems
            .Where(i => i.Cart.UserId == userId)
            .OrderBy(i => i.Id)
            .Select(i => new
            {
                i.Id,
                i.VariantId,
                i.Quantity,
                i.Variant.ProductId,
                ProductName = i.Variant.Product.Name,
                ProductSlug = i.Variant.Product.Slug,
                OnSale = i.Variant.Status && i.Variant.Product.Status,
                i.Variant.Price,
                i.Variant.StockQty,
            })
            .ToListAsync(ct);
        if (rows.Count == 0)
            return new CartView([]);

        var options = await VariantOptionLookup.LoadAsync(_db, rows.Select(r => r.VariantId).ToList(), ct);
        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var images = (await _db.ProductImages
                .Where(im => productIds.Contains(im.ProductId))
                .OrderBy(im => im.SortOrder).ThenBy(im => im.Id)
                .Select(im => new { im.ProductId, im.ImageUrl })
                .ToListAsync(ct))
            .GroupBy(im => im.ProductId)
            .ToDictionary(g => g.Key, g => g.First().ImageUrl);

        return new CartView(rows.Select(r =>
        {
            var configuration = options.ConfigurationName(r.VariantId, r.ProductName);
            return new CartLine(
                r.Id,
                r.VariantId,
                r.ProductId,
                r.ProductName,
                r.ProductSlug,
                configuration,
                CatalogSlug.From(configuration),
                options.Get(r.VariantId, "color"),
                options.Get(r.VariantId, "region"),
                images.GetValueOrDefault(r.ProductId),
                r.Price,
                r.Quantity,
                r.StockQty,
                Problem(r.OnSale, r.Price, r.StockQty, r.Quantity));
        }).ToList());
    }

    public Task<int> CountAsync(int userId, CancellationToken ct = default) =>
        _db.CartItems.Where(i => i.Cart.UserId == userId).SumAsync(i => i.Quantity, ct);

    private IQueryable<CartItem> OwnLine(int userId, int itemId) =>
        _db.CartItems.Where(i => i.Id == itemId && i.Cart.UserId == userId);

    // The one rule for whether a variant can go into a cart; Problem reuses it
    // for lines already there.
    private static CartOutcome? Refusal(bool onSale, decimal? price, int stock) =>
        !onSale ? CartOutcome.Unavailable
        : price is null ? CartOutcome.NoPrice
        : stock <= 0 ? CartOutcome.OutOfStock
        : null;

    private static CartLineProblem Problem(bool onSale, decimal? price, int stock, int quantity) =>
        Refusal(onSale, price, stock) switch
        {
            CartOutcome.Unavailable => CartLineProblem.Unavailable,
            CartOutcome.NoPrice => CartLineProblem.NoPrice,
            CartOutcome.OutOfStock => CartLineProblem.OutOfStock,
            _ => stock < quantity ? CartLineProblem.NotEnoughStock : CartLineProblem.None,
        };
}
