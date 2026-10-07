using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppleStore.Tests;

// One SQLite in-memory database for the shop's service tests (cart,
// checkout). Each context opened here shares it, so a second context can
// play another request; interceptors let a test act just before a write.
public sealed class ShopTestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ShopTestDb()
    {
        _connection.Open();
        using var db = Context();
        db.Database.EnsureCreated();
    }

    public AppDbContext Context(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options);

    public static User NewUser(string email) => new()
    {
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = email,
        Role = UserRole.Customer,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    public void Dispose() => _connection.Dispose();
}
