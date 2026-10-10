using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class StockService : IStockService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public StockService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<StockReceiptResult> ReceiveAsync(StockReceiptInput input, int? userId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<StockReceiptSummary>> ReceiptsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<StockReceiptView?> ReceiptAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<StockLevelRow>> LevelsAsync(string? search = null, CancellationToken ct = default) => throw new NotImplementedException();
}
