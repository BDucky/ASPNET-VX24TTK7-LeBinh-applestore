using System.Data.Common;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppleStore.Infrastructure.Identity;

public enum AdminSeedResult
{
    Created,
    AlreadyPresent,
    NotConfigured,
    Rejected,
    DatabaseUnavailable,
}

// Creates the first admin account at startup, through UserManager so the
// same password policy and unique-email rule apply as for customers. It only
// ever creates: an existing admin is never changed, and an email that
// already belongs to someone else is never promoted.
public sealed class AdminSeeder
{
    private readonly UserManager<User> _users;
    private readonly AppDbContext _db;
    private readonly SeedAdminOptions _options;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(UserManager<User> users, AppDbContext db, IOptions<SeedAdminOptions> options, ILogger<AdminSeeder> logger)
    {
        _users = users;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdminSeedResult> SeedAsync(CancellationToken ct = default)
    {
        try
        {
            if (await _db.Users.AnyAsync(u => u.Role == UserRole.Admin, ct))
                return AdminSeedResult.AlreadyPresent;

            if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
            {
                _logger.LogWarning(
                    "No admin account exists. Set SeedAdmin:Email and SeedAdmin:Password (user-secrets or environment variables) to create one at startup.");
                return AdminSeedResult.NotConfigured;
            }

            var email = _options.Email.Trim();
            var now = DateTime.UtcNow;
            var admin = new User
            {
                Email = email,
                FullName = string.IsNullOrWhiteSpace(_options.FullName) ? SeedAdminOptions.DefaultFullName : _options.FullName.Trim(),
                Role = UserRole.Admin,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var result = await _users.CreateAsync(admin, _options.Password);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Admin account {Email} was not created: {Errors}",
                    email, string.Join(" ", result.Errors.Select(e => e.Description)));
                return AdminSeedResult.Rejected;
            }

            _logger.LogInformation("Admin account {Email} created from the SeedAdmin settings.", email);
            return AdminSeedResult.Created;
        }
        catch (DbException ex)
        {
            _logger.LogError(ex, "Admin seeding skipped: the database could not be read. Run \"dotnet ef database update\" first.");
            return AdminSeedResult.DatabaseUnavailable;
        }
    }
}
