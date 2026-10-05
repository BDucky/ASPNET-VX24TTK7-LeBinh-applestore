using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Real Identity (UserManager, store, claims factory) over the SQLite
// in-memory fixture, configured by the same extension the web app uses.
public sealed class IdentityTestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public SqliteInMemoryFixture Fixture { get; }
    public UserManager<User> UserManager { get; }
    public IUserClaimsPrincipalFactory<User> ClaimsFactory { get; }

    public IdentityTestHost()
    {
        Fixture = new SqliteInMemoryFixture();
        Fixture.Context.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<AppDbContext>(Fixture.Context);
        services.AddAppleStoreIdentityCore();
        _provider = services.BuildServiceProvider();

        UserManager = _provider.GetRequiredService<UserManager<User>>();
        ClaimsFactory = _provider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        Fixture.Dispose();
    }
}
