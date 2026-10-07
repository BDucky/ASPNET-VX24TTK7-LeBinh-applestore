using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// A variant's options (config, color, region) by option-type code, loaded in
// one query for a set of variants. Shared by the catalog and the cart so both
// name a variant the same way.
internal sealed class VariantOptionLookup
{
    private readonly ILookup<int, (string Code, string Value)> _options;

    private VariantOptionLookup(ILookup<int, (string Code, string Value)> options) => _options = options;

    public static async Task<VariantOptionLookup> LoadAsync(AppDbContext db, IReadOnlyCollection<int> variantIds, CancellationToken ct)
    {
        var rows = await db.VariantOptions
            .Where(o => variantIds.Contains(o.VariantId))
            .Select(o => new { o.VariantId, o.OptionType.Code, o.OptionValue.Value })
            .ToListAsync(ct);
        return new VariantOptionLookup(rows.ToLookup(r => r.VariantId, r => (r.Code, r.Value)));
    }

    public string? Get(int variantId, string code) =>
        _options[variantId].Where(o => o.Code == code).Select(o => o.Value).FirstOrDefault();

    // A variant with no "config" option belongs to one configuration named
    // after its product (items sold as a single model).
    public string ConfigurationName(int variantId, string productName) => Get(variantId, "config") ?? productName;
}
