using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Services;

// Admins give accounts a role (Customer, Employee, Admin). Changing a role
// changes the security stamp, so the person's open sessions end.
public interface IAdminUserService
{
    Task<IReadOnlyList<UserAdminRow>> ListAsync(string? search, CancellationToken ct = default);
    Task<RoleChangeOutcome> ChangeRoleAsync(int actingAdminId, int userId, UserRole role, CancellationToken ct = default);
}
