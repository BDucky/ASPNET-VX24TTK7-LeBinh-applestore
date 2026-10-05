namespace AppleStore.Domain.Enums;

// The source report states the value domain is "0 to 4" (5 values) but only
// names 4. Resolved 2026-10-05 by the project owner: the 5th value is the
// "confirmed" step from chapter 2's order-tracking use case, placed between
// pending and shipping. See docs/data-model.md, "OrderStatus (resolved)".
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Shipping = 2,
    Completed = 3,
    Cancelled = 4,
}
