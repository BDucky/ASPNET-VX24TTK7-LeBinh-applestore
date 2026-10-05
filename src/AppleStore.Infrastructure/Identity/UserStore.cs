using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace AppleStore.Infrastructure.Identity;

// RED stub: every member throws until the GREEN commit.
public class UserStore :
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserSecurityStampStore<User>,
    IUserLockoutStore<User>
{
    private readonly AppDbContext _db;

    public UserStore(AppDbContext db) => _db = db;

    public Task<string> GetUserIdAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<string?> GetUserNameAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task SetUserNameAsync(User user, string? userName, CancellationToken ct) => throw new NotImplementedException();
    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken ct) => throw new NotImplementedException();
    public Task<IdentityResult> CreateAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<IdentityResult> UpdateAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<IdentityResult> DeleteAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<User?> FindByIdAsync(string userId, CancellationToken ct) => throw new NotImplementedException();
    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken ct) => throw new NotImplementedException();
    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken ct) => throw new NotImplementedException();
    public Task<string?> GetPasswordHashAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<bool> HasPasswordAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task SetEmailAsync(User user, string? email, CancellationToken ct) => throw new NotImplementedException();
    public Task<string?> GetEmailAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken ct) => throw new NotImplementedException();
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken ct) => throw new NotImplementedException();
    public Task<string?> GetSecurityStampAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken ct) => throw new NotImplementedException();
    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task ResetAccessFailedCountAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken ct) => throw new NotImplementedException();
    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken ct) => throw new NotImplementedException();

    public void Dispose() { }
}
