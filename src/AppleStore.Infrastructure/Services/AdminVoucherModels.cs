using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

public sealed record VoucherAdminRow(int Id, string? Code, VoucherDiscountType Type, decimal Value, decimal? MinOrderAmount,
    DateTime StartsAt, DateTime EndsAt, int? UsageLimit, int UsedCount, bool IsActive, IReadOnlyList<int> ProductIds, long Version);

public sealed record VoucherInput(string? Code, VoucherDiscountType Type, decimal Value, decimal? MinOrderAmount,
    DateTime StartsAt, DateTime EndsAt, int? UsageLimit, bool IsActive, IReadOnlyList<int> ProductIds);

public enum VoucherAdminOutcome
{
    Done,
    NotFound,
    MissingCode,
    CodeTaken,
    InvalidValue,
    InvalidPercent,
    InvalidMinimum,
    InvalidWindow,
    InvalidLimit,
    LimitBelowUsed,
    UnknownProduct,
    Changed,
    // Orders hold the code as text; renaming a used voucher would make a
    // cancelled order give its use back to the wrong voucher.
    CodeLocked,
}

public sealed record VoucherAdminResult(VoucherAdminOutcome Outcome, int? Id = null);

public sealed record UserAdminRow(int Id, string Email, string FullName, UserRole Role, DateTime CreatedAt);

public enum RoleChangeOutcome
{
    Done,
    NotFound,
    OwnAccount,
    InvalidRole,
}
