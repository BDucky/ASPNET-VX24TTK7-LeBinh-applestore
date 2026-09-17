using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? TxnId { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ProviderRaw { get; set; }
    public DateTime CreatedAt { get; set; }

    public Order Order { get; set; } = null!;
}
