namespace AppleStore.Domain.Entities;

// Five wrong phone numbers against one order number lock tracking for that
// order (BM_ORDER_TRACK_01), the same rule the account lockout already uses.
// One row per order, created on its first wrong phone.
public class OrderTrackLockout
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int FailCount { get; set; }
    public DateTime? LockedUntil { get; set; }
}
