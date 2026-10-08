using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public Task<(SalesReport? Report, ReportProblem Problem)> SalesAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
