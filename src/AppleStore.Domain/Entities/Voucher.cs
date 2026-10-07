using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

// Not in the report's schema, which only stores Order.VoucherCode as text;
// added 2026-10-07 (owner's choice: a table, so admins can manage vouchers).
// Rules from BM_VOUCHER_01: percent or fixed, all products or some products
// (VoucherProduct rows), an optional minimum order, a usage limit and a time
// window. Code is stored upper case and matched without regard to case.
public class Voucher
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public VoucherDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
