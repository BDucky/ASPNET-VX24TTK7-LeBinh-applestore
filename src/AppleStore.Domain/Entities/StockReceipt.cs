namespace AppleStore.Domain.Entities;

// Not in the report's schema; added 2026-10-10 for use case 20 and
// BM_STOCK_01 (goods from a supplier, closing stock = opening stock +
// intake). One receipt per delivery, typed in by staff. FormKey is the
// hidden key of the form that saved it, unique, so a second press of the
// same form never receives the goods twice.
public class StockReceipt
{
    public int Id { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid FormKey { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? CreatedBy { get; set; }
    public List<StockReceiptLine> Lines { get; set; } = [];
}

// One variant on a receipt. OpeningStock and ClosingStock are the variant's
// stock just before and just after this line was received.
public class StockReceiptLine
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public int OpeningStock { get; set; }
    public int ClosingStock { get; set; }

    public StockReceipt Receipt { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
