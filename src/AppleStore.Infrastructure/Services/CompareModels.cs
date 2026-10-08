namespace AppleStore.Infrastructure.Services;

// One column of the compare page (use case 10). The attribute tables are
// still empty, so a column holds what every product already has: price,
// configurations, colours, regions, stock and its visible reviews.
public sealed record CompareColumn(
    int ProductId,
    string Name,
    string Slug,
    string CategoryName,
    string? ImageUrl,
    decimal? FromPrice,
    bool InStock,
    IReadOnlyList<string> Configurations,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Regions,
    double? AverageRating,
    int ReviewCount);

public enum CompareOutcome
{
    Added,
    AlreadyIn,
    Full,
    NotFound,
}

// Ids is the list to keep after the call: the old one when nothing was added.
public sealed record CompareResult(CompareOutcome Outcome, IReadOnlyList<int> Ids);

public interface ICompareService
{
    // Columns in the order of the ids; hidden or unknown products are left out.
    Task<IReadOnlyList<CompareColumn>> BuildAsync(IReadOnlyList<int> productIds, CancellationToken ct = default);

    Task<CompareResult> AddAsync(IReadOnlyList<int> productIds, int productId, CancellationToken ct = default);
}
