using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Moves orders along by OrderTransitions. Each change is a compare-and-swap
// on the status that was read, inside a transaction with what goes with it
// (a shipment, the cash recorded, stock and voucher use given back), so two
// people pressing buttons at once cannot apply one change twice.
public class OrderManagementService : IOrderManagementService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public OrderManagementService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    private sealed record Snapshot(int Id, int UserId, OrderStatus Status, OrderPaymentStatus PaymentStatus, PaymentMethod Method,
        decimal Total, decimal ShippingFee, string? VoucherCode);

    private Task<Snapshot?> SnapshotAsync(int orderId, CancellationToken ct) =>
        _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new Snapshot(o.Id, o.UserId, o.Status, o.PaymentStatus,
                _db.Payments.Where(p => p.OrderId == o.Id).OrderBy(p => p.Id).Select(p => p.Method).FirstOrDefault(),
                o.TotalAmount, o.ShippingFee, o.VoucherCode))
            .FirstOrDefaultAsync(ct);

    public Task<IReadOnlyList<OrderListItem>> ListAsync(OrderStatus? status, CancellationToken ct = default) =>
        ListAsync(_db.Orders.Where(o => status == null || o.Status == status), ct);

    public Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken ct = default) =>
        ListAsync(_db.Orders.Where(o => o.UserId == userId), ct);

    private async Task<IReadOnlyList<OrderListItem>> ListAsync(IQueryable<Order> orders, CancellationToken ct) =>
        await orders
            .OrderByDescending(o => o.Id)
            .Select(o => new OrderListItem(
                o.Id,
                o.CreatedAt,
                o.Status,
                o.PaymentStatus,
                _db.Payments.Where(p => p.OrderId == o.Id).OrderBy(p => p.Id).Select(p => p.Method).FirstOrDefault(),
                o.TotalAmount,
                o.ReceiverName,
                _db.OrderItems.Where(i => i.OrderId == o.Id).Sum(i => i.Quantity)))
            .ToListAsync(ct);

    public Task<OrderChangeResult> ConfirmAsync(int orderId, CancellationToken ct = default) =>
        ChangeAsync(orderId, OrderAction.Confirm, null, (_, _) => Task.CompletedTask, ct);

    public Task<OrderChangeResult> ShipAsync(int orderId, ShipmentInput shipment, CancellationToken ct = default)
    {
        var carrier = shipment.Carrier?.Trim();
        var tracking = shipment.TrackingNo?.Trim();
        if (string.IsNullOrEmpty(carrier) || string.IsNullOrEmpty(tracking))
            return Task.FromResult(new OrderChangeResult(OrderChangeOutcome.MissingTracking));

        return ChangeAsync(orderId, OrderAction.Ship, null, async (order, now) =>
        {
            _db.Shipments.Add(new Shipment
            {
                OrderId = order.Id,
                Carrier = carrier,
                TrackingNo = tracking,
                Status = ShipmentStatus.InTransit,
                Fee = order.ShippingFee,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await _db.SaveChangesAsync(ct);
        }, ct);
    }

    public Task<OrderChangeResult> CompleteAsync(int orderId, CancellationToken ct = default) =>
        ChangeAsync(orderId, OrderAction.Complete, null, async (order, now) =>
        {
            await _db.Shipments.Where(s => s.OrderId == order.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ShipmentStatus.Delivered).SetProperty(x => x.UpdatedAt, now), ct);
            if (order.Method != PaymentMethod.Cod)
                return;

            // Delivered cash on delivery means the cash was collected (agreed 2026-10-08).
            await _db.Orders.Where(o => o.Id == order.Id && o.PaymentStatus == OrderPaymentStatus.Unpaid)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, OrderPaymentStatus.Paid), ct);
            await _db.Payments.Where(p => p.OrderId == order.Id && p.Method == PaymentMethod.Cod && p.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, PaymentStatus.Success)
                    .SetProperty(p => p.PaidAmount, order.Total)
                    .SetProperty(p => p.PaidAt, now), ct);
        }, ct);

    public Task<OrderChangeResult> CancelAsync(int orderId, int? customerId, CancellationToken ct = default) =>
        ChangeAsync(orderId, OrderAction.Cancel, customerId, async (order, _) =>
        {
            var items = await _db.OrderItems.Where(i => i.OrderId == order.Id).Select(i => new { i.VariantId, i.Quantity }).ToListAsync(ct);
            foreach (var item in items)
            {
                await _db.ProductVariants.Where(v => v.Id == item.VariantId)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, v => v.StockQty + item.Quantity), ct);
            }
            if (order.VoucherCode is { } code)
            {
                await _db.Vouchers.Where(v => v.Code == code && v.UsedCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount - 1), ct);
            }
            // An open attempt can no longer be paid.
            await _db.Payments.Where(p => p.OrderId == order.Id && p.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed), ct);
        }, ct);

    // Checks the order against the rules, then swaps its status only if it is
    // still the one read, and runs what goes with the change in the same
    // transaction.
    private async Task<OrderChangeResult> ChangeAsync(int orderId, OrderAction action, int? customerId,
        Func<Snapshot, DateTime, Task> alongWith, CancellationToken ct)
    {
        var order = await SnapshotAsync(orderId, ct);
        if (order is null || (customerId is { } owner && order.UserId != owner))
            return new OrderChangeResult(OrderChangeOutcome.NotFound);
        if (!OrderTransitions.From(action, byStaff: customerId is null).Contains(order.Status))
            return new OrderChangeResult(OrderChangeOutcome.NotAllowed);
        if (OrderTransitions.NeedsPayment(action, order.Method, order.PaymentStatus))
            return new OrderChangeResult(OrderChangeOutcome.NeedsPayment);

        var now = _time.GetUtcNow().UtcDateTime;
        var to = OrderTransitions.To(action);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var swapped = await _db.Orders
            .Where(o => o.Id == order.Id && o.Status == order.Status && o.PaymentStatus == order.PaymentStatus)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, to).SetProperty(o => o.UpdatedAt, now), ct);
        if (swapped != 1)
            return new OrderChangeResult(OrderChangeOutcome.NotAllowed);

        await alongWith(order, now);
        await tx.CommitAsync(ct);
        return new OrderChangeResult(OrderChangeOutcome.Done,
            RefundDue: action == OrderAction.Cancel && order.PaymentStatus == OrderPaymentStatus.Paid);
    }

    public async Task<TrackingResult?> TrackAsync(int orderId, string? phone, CancellationToken ct = default)
    {
        var given = Digits(phone);
        if (given.Length == 0)
            return null;

        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.Id, o.CreatedAt, o.Status, o.Phone })
            .FirstOrDefaultAsync(ct);
        if (order is null || Digits(order.Phone) != given)
            return null;

        var shipment = await _db.Shipments.AsNoTracking()
            .Where(s => s.OrderId == order.Id)
            .OrderByDescending(s => s.Id)
            .Select(s => new { s.Carrier, s.TrackingNo, s.Status })
            .FirstOrDefaultAsync(ct);
        return new TrackingResult(order.Id, order.CreatedAt, order.Status, shipment?.Carrier, shipment?.TrackingNo, shipment?.Status);
    }

    // Spaces, dots and dashes are how people write phone numbers, not part of them.
    private static string Digits(string? phone) => new((phone ?? "").Where(char.IsAsciiDigit).ToArray());
}
