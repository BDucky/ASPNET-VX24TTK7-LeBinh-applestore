using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Formatting;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Use case 21, BM_INVOICE_01. An in-person sale is an order (owner's choice
// 2026-10-10): channel InStore, paid and completed at once, so the invoice,
// the revenue report and a customer's orders cover it. Prices are the shop's
// own (SalePrices, a voucher through SaleRules) and include VAT, so there is
// no tax line and no shipping. The sale is all or nothing in one
// transaction, at the total the staff member saw. Claude's: cash or bank
// transfer only; an email names an existing account or is refused; the
// form's key is unique so one form never sells twice.
public class InStoreSaleService : IInStoreSaleService
{
    public const string WalkIn = "Walk-in customer";
    private const string Counter = "In store";

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public InStoreSaleService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<SaleQuote> QuoteAsync(IReadOnlyList<SaleLineInput> lines, string? voucherCode, CancellationToken ct = default)
    {
        var code = string.IsNullOrWhiteSpace(voucherCode) ? null : voucherCode.Trim().ToUpperInvariant();
        SaleQuote Problem(SaleOutcome problem, string? sku = null, IReadOnlyList<SaleQuoteLine>? quoted = null) =>
            new(quoted ?? [], 0m, 0m, 0m, code, VoucherProblem.None, problem, sku);

        if (lines.Count == 0)
            return Problem(SaleOutcome.NoLines);
        if (lines.Any(l => l.Quantity < 1))
            return Problem(SaleOutcome.InvalidQuantity);

        var ids = lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await _db.ProductVariants.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, v.SKU, v.ProductId, ProductName = v.Product.Name, v.Price, v.StockQty, OnSale = v.Status && v.Product.Status })
            .ToDictionaryAsync(v => v.Id, ct);
        if (variants.Count != ids.Count)
            return Problem(SaleOutcome.UnknownVariant);
        if (lines.GroupBy(l => l.VariantId).FirstOrDefault(g => g.Count() > 1) is { } twice)
            return Problem(SaleOutcome.DuplicateSku, variants[twice.Key].SKU);

        var prices = await SalePrices.LoadAsync(_db, _time.GetUtcNow().UtcDateTime, ct);
        var options = await VariantOptionLookup.LoadAsync(_db, ids, ct);
        var quoted = lines.Select(l =>
        {
            var v = variants[l.VariantId];
            var sale = prices.For(v.ProductId, v.Price);
            return (Line: new SaleQuoteLine(v.Id, v.SKU, v.ProductName,
                VariantText.Options(options.Get(v.Id, "config"), options.Get(v.Id, "color"), options.Get(v.Id, "region")),
                sale.Price, sale.WasPrice, l.Quantity, v.StockQty), v.ProductId, v.OnSale);
        }).ToList();
        var shown = quoted.Select(q => q.Line).ToList();

        if (quoted.FirstOrDefault(q => !q.OnSale || q.Line.UnitPrice is null) is { Line: not null } notForSale)
            return Problem(SaleOutcome.NotForSale, notForSale.Line.Sku, shown);

        // Priced and the voucher checked even when stock is short, so the
        // staff member sees every problem at once.
        var subtotal = shown.Sum(l => l.LineTotal!.Value);
        var check = code is null
            ? new SaleRules.VoucherCheck(0m, VoucherProblem.None, null, null)
            : await SaleRules.CheckVoucherAsync(_db, _time.GetUtcNow().UtcDateTime, code, quoted.Select(q => (q.ProductId, q.Line.LineTotal!.Value)).ToList(), subtotal, ct);
        var shortLine = shown.FirstOrDefault(l => l.Quantity > l.StockQty);
        return new SaleQuote(shown, subtotal, check.Discount, subtotal - check.Discount, code, check.Problem,
            shortLine is null ? SaleOutcome.Done : SaleOutcome.OutOfStock, shortLine?.Sku);
    }

    public async Task<SaleResult> SellAsync(SaleInput input, decimal expectedTotal, int staffId, CancellationToken ct = default)
    {
        if (input.Method is not (PaymentMethod.Cash or PaymentMethod.BankTransfer))
            return new SaleResult(SaleOutcome.InvalidMethod);
        int? customerId = null;
        if (!string.IsNullOrWhiteSpace(input.CustomerEmail))
        {
            var normalized = input.CustomerEmail.Trim().ToUpperInvariant();
            customerId = await _db.Users.Where(u => u.NormalizedEmail == normalized).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
            if (customerId is null)
                return new SaleResult(SaleOutcome.UnknownCustomer);
        }
        if (await SoldWithAsync(input.FormKey, ct) is { } earlier)
            return new SaleResult(SaleOutcome.AlreadySold, earlier);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var quote = await QuoteAsync(input.Lines, input.VoucherCode, ct);
        if (quote.Problem != SaleOutcome.Done)
            return new SaleResult(quote.Problem, Sku: quote.ProblemSku);
        if (quote.VoucherCode is not null && quote.VoucherProblem != VoucherProblem.None)
            return new SaleResult(SaleOutcome.VoucherRefused, VoucherProblem: quote.VoucherProblem);
        if (quote.Total != expectedTotal)
            return new SaleResult(SaleOutcome.TotalChanged);

        foreach (var line in quote.Lines.OrderBy(l => l.VariantId))
        {
            if (!await SaleRules.TryTakeStockAsync(_db, line.VariantId, line.Quantity, ct))
            {
                await tx.RollbackAsync(ct);
                return new SaleResult(SaleOutcome.OutOfStock, Sku: line.Sku);
            }
        }
        if (quote.VoucherCode is { } code)
        {
            var id = await _db.Vouchers.Where(v => v.Code == code && v.Kind == VoucherKind.Code).Select(v => v.Id).FirstAsync(ct);
            if (!await SaleRules.TryUseVoucherAsync(_db, id, ct))
            {
                await tx.RollbackAsync(ct);
                return new SaleResult(SaleOutcome.VoucherRefused, VoucherProblem: VoucherProblem.UsedUp);
            }
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            UserId = customerId,
            Channel = OrderChannel.InStore,
            SoldByUserId = staffId,
            FormKey = input.FormKey,
            Status = OrderStatus.Completed,
            PaymentStatus = OrderPaymentStatus.Paid,
            Subtotal = quote.Subtotal,
            DiscountAmount = quote.Discount,
            ShippingFee = 0m,
            TotalAmount = quote.Total,
            VoucherCode = quote.VoucherCode,
            ReceiverName = string.IsNullOrWhiteSpace(input.CustomerName) ? WalkIn : input.CustomerName.Trim(),
            Phone = input.CustomerPhone?.Trim() ?? string.Empty,
            AddressLine = Counter,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Orders.Add(order);
        var products = await _db.ProductVariants.Where(v => quote.Lines.Select(l => l.VariantId).Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.ProductId, ct);
        _db.OrderItems.AddRange(quote.Lines.Select(l => new OrderItem
        {
            Order = order,
            ProductId = products[l.VariantId],
            VariantId = l.VariantId,
            Price = l.UnitPrice!.Value,
            Quantity = l.Quantity,
        }));
        _db.Payments.Add(new Payment { Order = order, Method = input.Method, Status = PaymentStatus.Success, PaidAmount = quote.Total, PaidAt = now, CreatedAt = now });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same form sold a moment earlier (unique FormKey): undo this one.
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return await SoldWithAsync(input.FormKey, ct) is { } other
                ? new SaleResult(SaleOutcome.AlreadySold, other)
                : throw new InvalidOperationException("An in-store sale could not be saved.");
        }
        await tx.CommitAsync(ct);
        return new SaleResult(SaleOutcome.Done, order.Id);
    }

    public Task<int?> OrderForKeyAsync(Guid formKey, CancellationToken ct = default) => SoldWithAsync(formKey, ct);

    private Task<int?> SoldWithAsync(Guid formKey, CancellationToken ct) =>
        _db.Orders.Where(o => o.FormKey == formKey).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
}
