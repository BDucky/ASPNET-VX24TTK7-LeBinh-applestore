using System.Globalization;
using System.Text.Json;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AppleStore.Infrastructure.Payments;

// Online payment for an order placed with VNPay or MoMo. Each try is one
// Payments row; the gateway's answer is trusted only when its signature,
// payment and amount all check out. Every change is conditional on the state
// it replaces, so answers arriving twice or out of order cannot undo a
// payment or count it twice.
public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentGateway? _gateway;

    // No gateway registered means only cash on delivery is offered.
    public PaymentService(AppDbContext db, TimeProvider time, ILogger<PaymentService> logger, IPaymentGateway? gateway = null)
    {
        _db = db;
        _time = time;
        _logger = logger;
        _gateway = gateway;
    }

    public bool OnlineAvailable => _gateway is not null;

    public async Task<PayStart> StartAsync(int userId, int orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Where(o => o.Id == orderId && o.UserId == userId)
            .Select(o => new { o.Id, o.TotalAmount, o.PaymentStatus })
            .FirstOrDefaultAsync(ct);
        if (order is null)
            return new PayStart(PayStartOutcome.NotFound);

        // Not tracked: the answers update rows with ExecuteUpdate, which a
        // tracked copy would not see.
        var latest = await _db.Payments.AsNoTracking().Where(p => p.OrderId == orderId).OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (latest is null || latest.Method == PaymentMethod.Cod)
            return new PayStart(PayStartOutcome.NotOnline);
        if (order.PaymentStatus == OrderPaymentStatus.Paid)
            return new PayStart(PayStartOutcome.AlreadyPaid);
        if (_gateway is null)
            return new PayStart(PayStartOutcome.Unavailable);

        // The open attempt is reused; after a failure or a cancel, a new one.
        var attempt = latest;
        if (latest.Status != PaymentStatus.Pending)
        {
            attempt = new Payment { OrderId = order.Id, Method = latest.Method, Status = PaymentStatus.Pending, CreatedAt = _time.GetUtcNow().UtcDateTime };
            _db.Payments.Add(attempt);
            await _db.SaveChangesAsync(ct);
        }

        return new PayStart(PayStartOutcome.Ready, _gateway.CreatePaymentUrl(new PaymentRequest(attempt.Id, order.Id, order.TotalAmount, attempt.Method)));
    }

    public async Task<CallbackResult> HandleAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct = default)
    {
        if (_gateway is null || !_gateway.Verify(fields))
            return Rejected("signature does not verify", fields);
        if (!fields.TryGetValue("paymentId", out var idText) || !int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var paymentId)
            || !fields.TryGetValue("amount", out var amountText) || !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || !fields.TryGetValue("result", out var code))
            return Rejected("a field is missing", fields);

        var payment = await _db.Payments
            .Where(p => p.Id == paymentId)
            .Select(p => new { p.OrderId, p.Method, Total = p.Order.TotalAmount })
            .FirstOrDefaultAsync(ct);
        if (payment is null || payment.Method == PaymentMethod.Cod)
            return Rejected("no online payment with that id", fields);
        if (amount != payment.Total)
            return Rejected("the amount is not the order total", fields);

        var raw = JsonSerializer.Serialize(fields);
        fields.TryGetValue("txnId", out var txnId);

        if (code == PaymentResultCode.Success)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            // A success wins over an earlier failure: the money was taken.
            var marked = await _db.Payments
                .Where(p => p.Id == paymentId && p.Status != PaymentStatus.Success)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, PaymentStatus.Success)
                    .SetProperty(p => p.TxnId, txnId)
                    .SetProperty(p => p.PaidAmount, amount)
                    .SetProperty(p => p.PaidAt, _time.GetUtcNow().UtcDateTime)
                    .SetProperty(p => p.ProviderRaw, raw), ct);
            if (marked == 0)
                return new CallbackResult(CallbackOutcome.AlreadyPaid, payment.OrderId);

            var paid = await _db.Orders
                .Where(o => o.Id == payment.OrderId && o.PaymentStatus == OrderPaymentStatus.Unpaid)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, OrderPaymentStatus.Paid)
                    .SetProperty(o => o.UpdatedAt, _time.GetUtcNow().UtcDateTime), ct);
            await tx.CommitAsync(ct);
            if (paid == 1)
                return new CallbackResult(CallbackOutcome.Paid, payment.OrderId);

            _logger.LogWarning("Order {OrderId} was paid twice; payment {PaymentId} needs a refund", payment.OrderId, paymentId);
            return new CallbackResult(CallbackOutcome.PaidTwice, payment.OrderId);
        }

        // A failure only settles an open attempt; it never undoes a success.
        var failed = await _db.Payments
            .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed).SetProperty(p => p.ProviderRaw, raw), ct);
        if (failed == 0 && await _db.Payments.AnyAsync(p => p.Id == paymentId && p.Status == PaymentStatus.Success, ct))
            return new CallbackResult(CallbackOutcome.AlreadyPaid, payment.OrderId);
        return new CallbackResult(code == PaymentResultCode.Cancelled ? CallbackOutcome.Cancelled : CallbackOutcome.Failed, payment.OrderId);
    }

    private CallbackResult Rejected(string reason, IReadOnlyDictionary<string, string> fields)
    {
        _logger.LogWarning("Payment answer rejected: {Reason}. Fields: {Fields}", reason, JsonSerializer.Serialize(fields));
        return new CallbackResult(CallbackOutcome.Rejected);
    }
}
