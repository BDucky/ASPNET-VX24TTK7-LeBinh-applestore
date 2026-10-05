using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Identity;

// Lets ASP.NET Core Identity (UserManager, SignInManager) work on the report's
// own Users table instead of the AspNetUsers tables Identity would otherwise
// create. The email is the user name. Roles come from User.Role and are added
// as claims by AppUserClaimsPrincipalFactory, so no role store is needed.
public class UserStore :
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserSecurityStampStore<User>,
    IUserLockoutStore<User>
{
    private readonly AppDbContext _db;

    public UserStore(AppDbContext db) => _db = db;

    public Task<string> GetUserIdAsync(User user, CancellationToken ct) => Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(User user, CancellationToken ct) => Task.FromResult<string?>(user.Email);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken ct)
    {
        user.Email = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken ct) => Task.FromResult<string?>(user.NormalizedEmail);

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken ct)
    {
        user.NormalizedEmail = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken ct)
    {
        _db.Users.Add(user);
        return await SaveAsync(ct);
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken ct)
    {
        user.UpdatedAt = DateTime.UtcNow;
        if (_db.Entry(user).State == EntityState.Detached)
            _db.Users.Update(user);
        return await SaveAsync(ct);
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken ct)
    {
        _db.Users.Remove(user);
        return await SaveAsync(ct);
    }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken ct) =>
        int.TryParse(userId, out var id) ? await _db.Users.FindAsync([id], ct) : null;

    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
        FindByEmailAsync(normalizedUserName, ct);

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken ct)
    {
        user.PasswordHash = passwordHash ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken ct) => Task.FromResult<string?>(user.PasswordHash);

    public Task<bool> HasPasswordAsync(User user, CancellationToken ct) => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    public Task SetEmailAsync(User user, string? email, CancellationToken ct) => SetUserNameAsync(user, email, ct);

    public Task<string?> GetEmailAsync(User user, CancellationToken ct) => GetUserNameAsync(user, ct);

    // A User row only exists after the OTP sent to its email was confirmed
    // (see RegistrationService), so every stored email is confirmed.
    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken ct) => Task.FromResult(true);

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken ct) => Task.CompletedTask;

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        _db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken ct) => GetNormalizedUserNameAsync(user, ct);

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken ct) =>
        SetNormalizedUserNameAsync(user, normalizedEmail, ct);

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken ct)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(User user, CancellationToken ct) => Task.FromResult<string?>(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken ct) => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken ct)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken ct) => Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(User user, CancellationToken ct)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken ct) => Task.FromResult(user.AccessFailedCount);

    // Every account can be locked out; there is no per-user opt-out column.
    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken ct) => Task.FromResult(true);

    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken ct) => Task.CompletedTask;

    public void Dispose()
    {
    }

    // A unique-index violation (two writes racing for one email) comes back
    // as a failed IdentityResult rather than an exception.
    private async Task<IdentityResult> SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }
        catch (DbUpdateException ex)
        {
            return IdentityResult.Failed(new IdentityError { Code = "DbUpdateFailed", Description = ex.InnerException?.Message ?? ex.Message });
        }
    }
}
