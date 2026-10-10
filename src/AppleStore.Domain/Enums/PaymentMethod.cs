namespace AppleStore.Domain.Enums;

// Ordinal values are an implementation choice; the report names these three
// methods (COD, VNPay, MoMo) but never numbers them.
public enum PaymentMethod
{
    Cod = 0,
    VnPay = 1,
    MoMo = 2,
    // At the counter (use case 21, added 2026-10-10): paid on the spot.
    Cash = 3,
    BankTransfer = 4,
}
