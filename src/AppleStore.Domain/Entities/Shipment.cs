using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Carrier { get; set; } = string.Empty;
    public string? TrackingNo { get; set; }
    public ShipmentStatus Status { get; set; }
    public decimal Fee { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Order Order { get; set; } = null!;
}
