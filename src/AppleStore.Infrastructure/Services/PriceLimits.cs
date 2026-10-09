namespace AppleStore.Infrastructure.Services;

// The largest price the schema stores: decimal(12,2). SQLite would keep a
// bigger one as text, SQL Server would refuse it, so every price write
// checks against this one number (review 2026-10-09).
public static class PriceLimits
{
    public const decimal Max = 9_999_999_999.99m;
}
