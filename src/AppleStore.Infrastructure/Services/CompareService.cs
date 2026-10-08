using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class CompareService : ICompareService
{
    // Three, like Apple's own compare page (owner's decision 2026-10-08).
    public const int MaxItems = 3;

    private readonly AppDbContext _db;
    private readonly IReviewService _reviews;

    public CompareService(AppDbContext db, IReviewService reviews)
    {
        _db = db;
        _reviews = reviews;
    }

    public Task<IReadOnlyList<CompareColumn>> BuildAsync(IReadOnlyList<int> productIds, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<CompareResult> AddAsync(IReadOnlyList<int> productIds, int productId, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
