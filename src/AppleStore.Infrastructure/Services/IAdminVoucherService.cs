using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// Use cases 29-31. An order keeps its voucher code as text, so deleting a
// voucher never changes an order.
public interface IAdminVoucherService
{
    // Every call names the kind of row it may see or change: the promotions
    // page (open to employees) can never reach a code voucher, nor the other way.
    Task<IReadOnlyList<VoucherAdminRow>> ListAsync(VoucherKind kind, CancellationToken ct = default);
    Task<VoucherAdminRow?> GetAsync(int voucherId, VoucherKind kind, CancellationToken ct = default);
    Task<VoucherAdminResult> CreateAsync(VoucherInput input, CancellationToken ct = default);
    Task<VoucherAdminResult> UpdateAsync(int voucherId, VoucherInput input, long version, CancellationToken ct = default);
    Task<VoucherAdminResult> DeleteAsync(int voucherId, VoucherKind kind, CancellationToken ct = default);
}
