using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly AppDbContext _db;

    public CartService(AppDbContext db)
    {
        _db = db;
    }

    public Task<CartResult> AddAsync(int userId, int variantId, int quantity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<CartResult> SetQuantityAsync(int userId, int itemId, int quantity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<CartResult> RemoveAsync(int userId, int itemId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<CartView> GetAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
}
