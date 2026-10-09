namespace AppleStore.Infrastructure.Services;

// Use case 8, filter by price. Preset bands rather than typed amounts, and a
// product belongs to a band when one of its active variants is priced inside
// it (owner's decisions 2026-10-09; the amounts are Claude's, accepted).
public enum PriceBand
{
    Any,
    Under10M,
    From10MTo20M,
    From20MTo40M,
    Over40M,
}

public sealed record PriceBandInfo(PriceBand Band, string Label, decimal? Min, decimal? Max);

public static class PriceBands
{
    // The one table: Min is included, Max is not, so a price on a boundary
    // belongs to exactly one band.
    public static readonly IReadOnlyList<PriceBandInfo> All =
    [
        new(PriceBand.Under10M, "Under 10 million", null, 10_000_000m),
        new(PriceBand.From10MTo20M, "10 to 20 million", 10_000_000m, 20_000_000m),
        new(PriceBand.From20MTo40M, "20 to 40 million", 20_000_000m, 40_000_000m),
        new(PriceBand.Over40M, "40 million and over", 40_000_000m, null),
    ];

    // Null for Any, and for a value outside the enum (a hand-edited URL).
    public static PriceBandInfo? Find(PriceBand band) => All.FirstOrDefault(b => b.Band == band);
}
