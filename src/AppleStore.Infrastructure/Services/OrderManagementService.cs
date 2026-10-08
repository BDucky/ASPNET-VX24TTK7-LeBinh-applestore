using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class OrderManagementService : IOrderManagementService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public OrderManagementService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<IReadOnlyList<OrderListItem>> ListAsync(OrderStatus? status, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OrderChangeResult> ConfirmAsync(int orderId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OrderChangeResult> ShipAsync(int orderId, ShipmentInput shipment, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OrderChangeResult> CompleteAsync(int orderId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OrderChangeResult> CancelAsync(int orderId, int? customerId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<TrackingResult?> TrackAsync(int orderId, string? phone, CancellationToken ct = default) => throw new NotImplementedException();
}
