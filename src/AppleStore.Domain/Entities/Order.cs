using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    // Null for a walk-in customer at the counter (use case 21, 2026-10-10).
    public int? UserId { get; set; }
    public OrderChannel Channel { get; set; }
    // In-person sales: the staff member who sold, and the sale form's key,
    // unique, so one form never sells twice.
    public int? SoldByUserId { get; set; }
    public Guid? FormKey { get; set; }
    public OrderStatus Status { get; set; }
    public OrderPaymentStatus PaymentStatus { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string? VoucherCode { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
    public User? SoldBy { get; set; }
}
