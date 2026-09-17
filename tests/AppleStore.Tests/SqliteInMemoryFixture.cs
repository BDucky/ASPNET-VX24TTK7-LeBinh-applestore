using AppleStore.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Tests;

// Standard EF Core SQLite in-memory testing pattern: the connection must stay
// open for the fixture's lifetime, since ":memory:" databases are dropped as
// soon as the last connection to them closes.
public class SqliteInMemoryFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Context { get; }

    public SqliteInMemoryFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
