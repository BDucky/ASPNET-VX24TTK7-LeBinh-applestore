using System.Net;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// The seeder as Program.cs runs it: settings from configuration, at startup.
// Each test builds its own factory with its own settings; the base class's
// default one is not used.
public class AdminSeedingStartupTests : WebFlowTestBase
{
    private static HttpClient NewClient(AppleStoreWebFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    [Fact]
    public async Task Startup_creates_the_admin_from_configuration_and_it_can_sign_in()
    {
        using var factory = new AppleStoreWebFactory(new Dictionary<string, string?>
        {
            ["SeedAdmin:Email"] = "admin@applestore.test",
            ["SeedAdmin:Password"] = "Seeded-Admin-1",
        });
        using var client = NewClient(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Users.SingleAsync();
            Assert.Equal(UserRole.Admin, admin.Role);
            Assert.Equal("admin@applestore.test", admin.Email);
        }

        var login = await LoginAsync(client, "admin@applestore.test", "Seeded-Admin-1");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);
    }

    // Meaningful when run with SEEDADMIN__EMAIL / SEEDADMIN__PASSWORD set in
    // the environment (see docs/verification.md): the test host must ignore them.
    [Fact]
    public async Task The_default_test_host_seeds_no_admin()
    {
        using var factory = new AppleStoreWebFactory();
        using var client = NewClient(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync());
    }
}
