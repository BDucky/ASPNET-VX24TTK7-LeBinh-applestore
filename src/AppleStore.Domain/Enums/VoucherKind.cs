namespace AppleStore.Domain.Enums;

// Added 2026-10-09 for use case 28 (owner's choice: promotions share the
// voucher table). A Code voucher is typed at checkout; an Automatic one is a
// promotion: no code, it lowers the price shown and charged while it runs.
public enum VoucherKind
{
    Code = 0,
    Automatic = 1,
}
