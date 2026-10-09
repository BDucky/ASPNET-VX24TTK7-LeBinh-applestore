using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

// Not in the report's schema; added 2026-10-09 for BM_PRICE_01's
// "price-change history log". One row per variant whose own price changed,
// written in the same transaction as the change. A null price means
// "Contact for price".
public class PriceChange
{
    public int Id { get; set; }
    public int VariantId { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public PriceChangeSource Source { get; set; }
    // Null when the account was later deleted, or for a change made by the system.
    public int? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public User? ChangedBy { get; set; }
}
