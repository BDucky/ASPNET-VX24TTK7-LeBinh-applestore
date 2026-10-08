using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

public class AdminUserService : IAdminUserService
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _users;

    public AdminUserService(AppDbContext db, UserManager<User> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IReadOnlyList<UserAdminRow>> ListAsync(string? search, CancellationToken ct = default)
    {
        var users = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = "%" + search.Trim() + "%";
            users = users.Where(u => EF.Functions.Like(u.Email, term) || EF.Functions.Like(u.FullName, term));
        }
        return await users.OrderBy(u => u.Email)
            .Select(u => new UserAdminRow(u.Id, u.Email, u.FullName, u.Role, u.CreatedAt))
            .ToListAsync(ct);
    }

    // An admin never changes their own role, so the admin area always keeps
    // the admin who is using it.
    public async Task<RoleChangeOutcome> ChangeRoleAsync(int actingAdminId, int userId, UserRole role, CancellationToken ct = default)
    {
        if (actingAdminId == userId)
            return RoleChangeOutcome.OwnAccount;
        if (!Enum.IsDefined(role))
            return RoleChangeOutcome.InvalidRole;
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return RoleChangeOutcome.NotFound;

        user.Role = role;
        // The new stamp ends the person's open sessions; their next sign-in carries the new role.
        var saved = await _users.UpdateSecurityStampAsync(user);
        if (!saved.Succeeded)
            throw new InvalidOperationException("Saving the role failed: " + string.Join(" ", saved.Errors.Select(e => e.Description)));
        return RoleChangeOutcome.Done;
    }
}
