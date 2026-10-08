namespace AppleStore.Infrastructure.Services;

// Admin edits use a row's UpdatedAt as its version: a save only lands while
// UpdatedAt is still what the admin saw. Two saves in the same clock tick
// would leave UpdatedAt unchanged and let a stale edit through, so the next
// value is always later than the one seen.
public static class RowVersion
{
    public static DateTime Next(long seen, DateTime now)
    {
        var previous = new DateTime(seen);
        return now > previous ? now : previous.AddTicks(1);
    }
}
