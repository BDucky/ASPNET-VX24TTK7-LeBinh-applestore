using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppleStore.Tests;

// The seeder runs with the real UserManager, UserStore and SQLite schema,
// so Identity's own password policy and unique-email rule decide.
public class AdminSeederTests
{
    private const string AdminEmail = "admin@applestore.test";
    private const string AdminPassword = "Seeded-Admin-1";

    private static (AdminSeeder Seeder, ListLogger<AdminSeeder> Log) NewSeeder(IdentityTestHost host, string? email, string? password)
    {
        var log = new ListLogger<AdminSeeder>();
        var options = Options.Create(new SeedAdminOptions { Email = email, Password = password, FullName = "Store Admin" });
        return (new AdminSeeder(host.UserManager, host.Fixture.Context, options, log), log);
    }

    private static async Task<User> CreateAsync(IdentityTestHost host, string email, string password, UserRole role)
    {
        var user = new User { Email = email, FullName = "Existing", Role = role, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var result = await host.UserManager.CreateAsync(user, password);
        Assert.True(result.Succeeded);
        return user;
    }

    private static void AssertPasswordNeverLogged(ListLogger<AdminSeeder> log, string password) =>
        Assert.DoesNotContain(log.Entries, e => e.Message.Contains(password, StringComparison.Ordinal));

    [Fact]
    public async Task Creates_the_admin_when_none_exists_and_settings_are_given()
    {
        using var host = new IdentityTestHost();
        var (seeder, log) = NewSeeder(host, AdminEmail, AdminPassword);

        var result = await seeder.SeedAsync();

        Assert.Equal(AdminSeedResult.Created, result);
        var admin = Assert.Single(host.Fixture.Context.Users);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(AdminEmail, admin.Email);
        Assert.Equal("Store Admin", admin.FullName);
        Assert.NotEqual(default, admin.CreatedAt);
        Assert.True(await host.UserManager.CheckPasswordAsync(admin, AdminPassword));
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Information && e.Message.Contains(AdminEmail));
        AssertPasswordNeverLogged(log, AdminPassword);
    }

    [Fact]
    public async Task Leaves_an_existing_admin_alone_and_adds_no_second_one()
    {
        using var host = new IdentityTestHost();
        var existing = await CreateAsync(host, "first.admin@applestore.test", "Original-Pass-1", UserRole.Admin);
        var (seeder, log) = NewSeeder(host, AdminEmail, AdminPassword);

        var result = await seeder.SeedAsync();

        Assert.Equal(AdminSeedResult.AlreadyPresent, result);
        Assert.Single(host.Fixture.Context.Users);
        Assert.True(await host.UserManager.CheckPasswordAsync(existing, "Original-Pass-1"));
        Assert.DoesNotContain(log.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Running_twice_keeps_one_admin_and_the_first_password()
    {
        using var host = new IdentityTestHost();
        await NewSeeder(host, AdminEmail, AdminPassword).Seeder.SeedAsync();

        var second = await NewSeeder(host, AdminEmail, "Changed-Pass-2").Seeder.SeedAsync();

        Assert.Equal(AdminSeedResult.AlreadyPresent, second);
        var admin = Assert.Single(host.Fixture.Context.Users);
        Assert.True(await host.UserManager.CheckPasswordAsync(admin, AdminPassword));
    }

    [Fact]
    public async Task An_existing_admin_with_no_settings_logs_no_warning()
    {
        using var host = new IdentityTestHost();
        await CreateAsync(host, "first.admin@applestore.test", "Original-Pass-1", UserRole.Admin);
        var (seeder, log) = NewSeeder(host, null, null);

        Assert.Equal(AdminSeedResult.AlreadyPresent, await seeder.SeedAsync());
        Assert.DoesNotContain(log.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(AdminEmail, null)]
    [InlineData(null, AdminPassword)]
    [InlineData("  ", AdminPassword)]
    [InlineData(AdminEmail, "")]
    public async Task Missing_settings_create_nothing_and_say_which_setting_is_needed(string? email, string? password)
    {
        using var host = new IdentityTestHost();
        var (seeder, log) = NewSeeder(host, email, password);

        var result = await seeder.SeedAsync();

        Assert.Equal(AdminSeedResult.NotConfigured, result);
        Assert.Empty(host.Fixture.Context.Users);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("SeedAdmin:Password"));
    }

    [Fact]
    public async Task A_password_identity_rejects_creates_nothing_and_logs_the_reason_not_the_password()
    {
        using var host = new IdentityTestHost();
        var (seeder, log) = NewSeeder(host, AdminEmail, "short");

        var result = await seeder.SeedAsync();

        Assert.Equal(AdminSeedResult.Rejected, result);
        Assert.Empty(host.Fixture.Context.Users);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("at least 8"));
        AssertPasswordNeverLogged(log, "short");
    }

    // Promoting a customer silently would hand admin rights to whoever
    // registered that address first, so the seeder refuses instead.
    [Fact]
    public async Task An_email_that_already_belongs_to_a_customer_is_not_promoted()
    {
        using var host = new IdentityTestHost();
        var customer = await CreateAsync(host, AdminEmail, "Customer-Pass-1", UserRole.Customer);
        var (seeder, log) = NewSeeder(host, AdminEmail.ToUpperInvariant(), AdminPassword);

        var result = await seeder.SeedAsync();

        Assert.Equal(AdminSeedResult.Rejected, result);
        var only = Assert.Single(host.Fixture.Context.Users);
        Assert.Equal(UserRole.Customer, only.Role);
        Assert.True(await host.UserManager.CheckPasswordAsync(customer, "Customer-Pass-1"));
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);
        AssertPasswordNeverLogged(log, AdminPassword);
    }

    [Fact]
    public async Task A_database_without_tables_is_reported_and_does_not_throw()
    {
        using var host = new IdentityTestHost(createSchema: false);
        var (seeder, log) = NewSeeder(host, AdminEmail, AdminPassword);

        var result = await seeder.SeedAsync();

        Assert.Equal(AdminSeedResult.DatabaseUnavailable, result);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("database update"));
    }
}
