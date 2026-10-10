namespace AppleStore.Domain.Enums;

// Added 2026-10-10 for use case 21 (owner's choice: an in-person sale is an
// order, so invoices, the revenue report and the stock rules cover it).
public enum OrderChannel
{
    Online = 0,
    InStore = 1,
}
