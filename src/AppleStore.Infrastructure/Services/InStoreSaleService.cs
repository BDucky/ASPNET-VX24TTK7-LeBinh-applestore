using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class InStoreSaleService : IInStoreSaleService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public InStoreSaleService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<SaleQuote> QuoteAsync(IReadOnlyList<SaleLineInput> lines, string? voucherCode, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<SaleResult> SellAsync(SaleInput input, decimal expectedTotal, int staffId, CancellationToken ct = default) => throw new NotImplementedException();
}
