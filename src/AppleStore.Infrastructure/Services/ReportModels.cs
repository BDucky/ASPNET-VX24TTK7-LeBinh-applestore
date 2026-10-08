namespace AppleStore.Infrastructure.Services;

// Revenue rule (agreed 2026-10-08, BM_CALC_REVENUE_01): an order counts once
// it is paid and not cancelled (online paid, or cash collected on delivery);
// its revenue is the sum of quantity x unit price minus the discount. Days
// are the shop's days, in Vietnam time (UTC+7).
public sealed record ReportDay(DateOnly Date, int Orders, int Items, decimal Sales, decimal Discount)
{
    public decimal Revenue => Sales - Discount;
}

// Sales: quantity x unit price, before order-level discounts.
public sealed record ReportProduct(int ProductId, string Name, int Quantity, decimal Sales);

public sealed record SalesReport(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<ReportDay> Days,
    IReadOnlyList<ReportProduct> Products,
    int OrdersPlaced,
    int OrdersPaid,
    int OrdersWaiting,
    int OrdersCancelled)
{
    public decimal Sales => Days.Sum(d => d.Sales);
    public decimal Discount => Days.Sum(d => d.Discount);
    public decimal Revenue => Days.Sum(d => d.Revenue);
    public int Items => Days.Sum(d => d.Items);
    public decimal AverageOrder => OrdersPaid == 0 ? 0m : Math.Round(Revenue / OrdersPaid, 0, MidpointRounding.AwayFromZero);
}

public enum ReportProblem
{
    None,
    EndBeforeStart,
}

public interface IReportService
{
    // From and To are inclusive shop days.
    Task<(SalesReport? Report, ReportProblem Problem)> SalesAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
