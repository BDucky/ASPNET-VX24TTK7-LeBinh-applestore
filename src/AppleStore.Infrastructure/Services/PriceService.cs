using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class PriceService : IPriceService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public PriceService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<PriceBatchResult> ApplyBatchAsync(PriceBatchInput input, int userId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<PriceChangeRow>> HistoryAsync(int? productId = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
