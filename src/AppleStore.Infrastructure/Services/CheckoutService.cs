using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Turns the cart into an order. Placing runs in one transaction: every write
// is conditional (stock still there, a voucher use still left, the cart
// lines still as read), and any that finds the state moved rolls all of it
// back, so a double submit or another shopper in between never yields a
// half-written order.
public class CheckoutService : ICheckoutService
{
    // Agreed 2026-10-07: the store advertises free standard shipping.
    private const decimal ShippingFee = 0m;

    private readonly AppDbContext _db;
    private readonly ICartService _cart;
    private readonly TimeProvider _time;

    public CheckoutService(AppDbContext db, ICartService cart, TimeProvider time)
    {
        _db = db;
        _cart = cart;
        _time = time;
    }

    public async Task<CheckoutQuote> QuoteAsync(int userId, string? voucherCode, CancellationToken ct = default) =>
        (await PriceAsync(userId, voucherCode, ct)).Quote;

    private sealed record Priced(CheckoutQuote Quote, int? VoucherId);

    private async Task<Priced> PriceAsync(int userId, string? voucherCode, CancellationToken ct)
    {
        var cart = await _cart.GetAsync(userId, ct);
        var code = string.IsNullOrWhiteSpace(voucherCode) ? null : voucherCode.Trim().ToUpperInvariant();
        var problem = cart.Lines.Count == 0 ? CheckoutProblem.CartEmpty
            : cart.Lines.Any(l => l.Problem != CartLineProblem.None) ? CheckoutProblem.CartHasProblems
            : CheckoutProblem.None;
        var subtotal = cart.Subtotal;

        var (discount, voucherProblem, minimum, voucherId) = code is null || problem != CheckoutProblem.None
            ? (0m, VoucherProblem.None, (decimal?)null, (int?)null)
            : await DiscountAsync(code, cart, subtotal, ct);

        return new Priced(
            new CheckoutQuote(cart, subtotal, discount, ShippingFee, subtotal - discount + ShippingFee, code, voucherProblem, problem, minimum),
            voucherId);
    }

    // BM_VOUCHER_01. Both ends of the window count as valid; the amount comes
    // from DiscountMath, taken from the lines the voucher applies to.
    private async Task<(decimal Discount, VoucherProblem Problem, decimal? Minimum, int? VoucherId)> DiscountAsync(
        string code, CartView cart, decimal subtotal, CancellationToken ct)
    {
        var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == code && v.Kind == VoucherKind.Code, ct);
        if (voucher is null)
            return (0m, VoucherProblem.NotFound, null, null);

        var now = _time.GetUtcNow().UtcDateTime;
        var refusal = !voucher.IsActive ? VoucherProblem.Inactive
            : now < voucher.StartsAt ? VoucherProblem.NotStarted
            : now > voucher.EndsAt ? VoucherProblem.Expired
            : voucher.UsageLimit is { } limit && voucher.UsedCount >= limit ? VoucherProblem.UsedUp
            : voucher.MinOrderAmount is { } min && subtotal < min ? VoucherProblem.BelowMinimum
            : VoucherProblem.None;
        if (refusal != VoucherProblem.None)
            return (0m, refusal, refusal == VoucherProblem.BelowMinimum ? voucher.MinOrderAmount : null, voucher.Id);

        var scope = await _db.VoucherProducts.Where(vp => vp.VoucherId == voucher.Id).Select(vp => vp.ProductId).ToListAsync(ct);
        var eligible = scope.Count == 0
            ? subtotal
            : cart.Lines.Where(l => scope.Contains(l.ProductId)).Sum(l => l.LineTotal ?? 0m);
        if (eligible == 0m)
            return (0m, VoucherProblem.NoEligibleProducts, null, voucher.Id);

        return (DiscountMath.Amount(voucher.DiscountType, voucher.DiscountValue, eligible), VoucherProblem.None, null, voucher.Id);
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(int userId, DeliveryInput delivery, string? voucherCode, decimal expectedTotal,
        PaymentMethod method = PaymentMethod.Cod, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var (quote, voucherId) = await PriceAsync(userId, voucherCode, ct);

        if (quote.Problem == CheckoutProblem.CartEmpty)
            return new PlaceOrderResult(PlaceOrderOutcome.CartEmpty);
        if (quote.Problem == CheckoutProblem.CartHasProblems)
            return new PlaceOrderResult(PlaceOrderOutcome.CartHasProblems);
        if (quote.VoucherCode is not null && quote.VoucherProblem != VoucherProblem.None)
            return new PlaceOrderResult(PlaceOrderOutcome.VoucherRefused, VoucherProblem: quote.VoucherProblem);
        if (quote.Total != expectedTotal)
            return new PlaceOrderResult(PlaceOrderOutcome.TotalChanged);

        foreach (var line in quote.Cart.Lines)
        {
            var taken = await _db.ProductVariants
                .Where(v => v.Id == line.VariantId && v.StockQty >= line.Quantity && v.Status && v.Product.Status)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQty, v => v.StockQty - line.Quantity), ct);
            if (taken != 1)
                return await RollBackAsync(tx, new PlaceOrderResult(PlaceOrderOutcome.OutOfStock, ProductName: line.ConfigurationName), ct);
        }

        if (voucherId is { } id)
        {
            var used = await _db.Vouchers
                .Where(v => v.Id == id && v.IsActive && (v.UsageLimit == null || v.UsedCount < v.UsageLimit))
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1), ct);
            if (used != 1)
                return await RollBackAsync(tx, new PlaceOrderResult(PlaceOrderOutcome.VoucherRefused, VoucherProblem: VoucherProblem.UsedUp), ct);
        }

        // Each line must still be there with the quantity that was priced; a
        // second submit or another tab finds it gone and changes nothing.
        foreach (var line in quote.Cart.Lines)
        {
            var removed = await _db.CartItems
                .Where(i => i.Id == line.ItemId && i.Quantity == line.Quantity && i.Cart.UserId == userId)
                .ExecuteDeleteAsync(ct);
            if (removed != 1)
                return await RollBackAsync(tx, new PlaceOrderResult(PlaceOrderOutcome.CartChanged), ct);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            PaymentStatus = OrderPaymentStatus.Unpaid,
            Subtotal = quote.Subtotal,
            DiscountAmount = quote.Discount,
            ShippingFee = quote.ShippingFee,
            TotalAmount = quote.Total,
            VoucherCode = voucherId is null ? null : quote.VoucherCode,
            ReceiverName = delivery.ReceiverName,
            Phone = delivery.Phone,
            AddressLine = delivery.AddressLine,
            Ward = delivery.Ward,
            District = delivery.District,
            City = delivery.City,
            Note = string.IsNullOrWhiteSpace(delivery.Note) ? null : delivery.Note,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Orders.Add(order);
        _db.OrderItems.AddRange(quote.Cart.Lines.Select(l => new OrderItem
        {
            Order = order,
            ProductId = l.ProductId,
            VariantId = l.VariantId,
            Price = l.UnitPrice!.Value,
            Quantity = l.Quantity,
        }));
        // The first attempt; an online one is settled by PaymentService.
        _db.Payments.Add(new Payment { Order = order, Method = method, Status = PaymentStatus.Pending, PaidAmount = 0m, CreatedAt = now });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new PlaceOrderResult(PlaceOrderOutcome.Placed, order.Id);
    }

    private static async Task<PlaceOrderResult> RollBackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, PlaceOrderResult result, CancellationToken ct)
    {
        await tx.RollbackAsync(ct);
        return result;
    }

    public Task<OrderSummary?> GetOrderAsync(int userId, int orderId, CancellationToken ct = default) =>
        LoadOrderAsync(orderId, userId, ct);

    public Task<OrderSummary?> GetOrderForStaffAsync(int orderId, CancellationToken ct = default) =>
        LoadOrderAsync(orderId, null, ct);

    // userId set: only that customer's order; null: any order (staff).
    private async Task<OrderSummary?> LoadOrderAsync(int orderId, int? userId, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && (userId == null || o.UserId == userId), ct);
        if (order is null)
            return null;

        var items = await _db.OrderItems
            .Where(i => i.OrderId == order.Id)
            .OrderBy(i => i.Id)
            .Select(i => new { i.VariantId, i.Price, i.Quantity, ProductName = i.Product.Name })
            .ToListAsync(ct);
        var options = await VariantOptionLookup.LoadAsync(_db, items.Select(i => i.VariantId).ToList(), ct);
        var method = await _db.Payments.Where(p => p.OrderId == order.Id).OrderBy(p => p.Id).Select(p => p.Method).FirstOrDefaultAsync(ct);
        var email = await _db.Users.Where(u => u.Id == order.UserId).Select(u => u.Email).FirstAsync(ct);
        var shipment = await _db.Shipments.AsNoTracking().Where(s => s.OrderId == order.Id).OrderByDescending(s => s.Id)
            .Select(s => new { s.Carrier, s.TrackingNo, s.Status }).FirstOrDefaultAsync(ct);

        return new OrderSummary(
            order.Id,
            order.CreatedAt,
            order.Status,
            order.PaymentStatus,
            method,
            items.Select(i => new OrderLineSummary(
                options.ConfigurationName(i.VariantId, i.ProductName),
                options.Get(i.VariantId, "color"),
                options.Get(i.VariantId, "region"),
                i.Price,
                i.Quantity)).ToList(),
            order.Subtotal,
            order.DiscountAmount,
            order.ShippingFee,
            order.TotalAmount,
            order.VoucherCode,
            order.ReceiverName,
            order.Phone,
            order.AddressLine,
            order.Ward,
            order.District,
            order.City,
            order.Note,
            email,
            shipment?.Carrier,
            shipment?.TrackingNo,
            shipment?.Status);
    }
}
