using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
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

    public Task<AdminSeedResult> SeedAsync(CancellationToken ct = default) => throw new NotImplementedException();
}
