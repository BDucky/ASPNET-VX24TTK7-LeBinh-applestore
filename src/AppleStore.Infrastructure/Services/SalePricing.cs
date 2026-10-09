using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

// The price a shopper sees and pays. WasPrice is the variant's own price when
// a promotion lowered it, PromotionName the promotion that did.
public sealed record SalePrice(decimal? Price, decimal? WasPrice = null, string? PromotionName = null);

public sealed class SalePrices
{
    public SalePrice For(int productId, decimal? basePrice) => new(basePrice);

    public static Task<SalePrices> LoadAsync(AppDbContext db, DateTime nowUtc, CancellationToken ct = default) =>
        Task.FromResult(new SalePrices());
}
