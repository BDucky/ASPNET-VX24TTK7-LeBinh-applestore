namespace AppleStore.Domain.Enums;

// The source report states the value domain is "0 to 4" (5 values) but only
// names these 4. Chapter 2's order-tracking use case separately lists 6
// narrative states (including a distinct "confirmed" step and a "returned"
// outcome) that don't map cleanly onto this numbering. Do not add a 5th value
// here without the team resolving the discrepancy first, see docs/data-model.md.
public enum OrderStatus
{
    Pending = 0,
    Shipping = 1,
    Completed = 2,
    Cancelled = 3,
}
