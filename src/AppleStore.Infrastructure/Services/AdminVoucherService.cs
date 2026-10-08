using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class AdminVoucherService : IAdminVoucherService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public AdminVoucherService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<IReadOnlyList<VoucherAdminRow>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<VoucherAdminRow?> GetAsync(int voucherId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<VoucherAdminResult> CreateAsync(VoucherInput input, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<VoucherAdminResult> UpdateAsync(int voucherId, VoucherInput input, long version, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<VoucherAdminResult> DeleteAsync(int voucherId, CancellationToken ct = default) => throw new NotImplementedException();
}
