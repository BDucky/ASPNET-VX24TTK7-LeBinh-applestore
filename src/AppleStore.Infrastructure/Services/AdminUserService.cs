using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

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

    public Task<IReadOnlyList<UserAdminRow>> ListAsync(string? search, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<RoleChangeOutcome> ChangeRoleAsync(int actingAdminId, int userId, UserRole role, CancellationToken ct = default) => throw new NotImplementedException();
}
