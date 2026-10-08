namespace AppleStore.Infrastructure.Services;

// Use cases 29-31. An order keeps its voucher code as text, so deleting a
// voucher never changes an order.
public interface IAdminVoucherService
{
    Task<IReadOnlyList<VoucherAdminRow>> ListAsync(CancellationToken ct = default);
    Task<VoucherAdminRow?> GetAsync(int voucherId, CancellationToken ct = default);
    Task<VoucherAdminResult> CreateAsync(VoucherInput input, CancellationToken ct = default);
    Task<VoucherAdminResult> UpdateAsync(int voucherId, VoucherInput input, long version, CancellationToken ct = default);
    Task<VoucherAdminResult> DeleteAsync(int voucherId, CancellationToken ct = default);
}
