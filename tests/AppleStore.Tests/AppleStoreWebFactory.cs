using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppleStore.Tests;

// Runs the real web app (routing, MVC, Identity, cookies, anti-forgery) in
// memory. Only two things are swapped: the database becomes a private SQLite
// in-memory one, and email is captured instead of logged.
//
// The schema exists before the app starts, as it does after
// "dotnet ef database update", because startup reads it (AdminSeeder).
// The SeedAdmin settings are blanked so the owner's user-secrets or
// environment variables never seed an admin into a test; a test that wants
// one passes its own settings.
public sealed class AppleStoreWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Dictionary<string, string?> _settings = new()
    {
        ["SeedAdmin:Email"] = "",
        ["SeedAdmin:Password"] = "",
    };

    public FakeEmailSender Email { get; } = new();

    public AppleStoreWebFactory(IReadOnlyDictionary<string, string?>? settings = null)
    {
        _connection.Open();
        using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
            db.Database.EnsureCreated();
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            _settings[key] = value;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(_settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
