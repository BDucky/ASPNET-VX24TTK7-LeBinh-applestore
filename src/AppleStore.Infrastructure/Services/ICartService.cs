namespace AppleStore.Infrastructure.Services;

// The signed-in user's cart (use cases 11-14). Every call is scoped to the
// user id, so a line of another user's cart is NotFound.
public interface ICartService
{
    Task<CartResult> AddAsync(int userId, int variantId, int quantity, CancellationToken ct = default);
    Task<CartResult> SetQuantityAsync(int userId, int itemId, int quantity, CancellationToken ct = default);
    Task<CartResult> RemoveAsync(int userId, int itemId, CancellationToken ct = default);
    Task<CartView> GetAsync(int userId, CancellationToken ct = default);
    Task<int> CountAsync(int userId, CancellationToken ct = default);
}
