using System.Security.Claims;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Identity;

namespace AppleStore.Tests;

public class UserStoreTests
{
    [Fact]
    public async Task CreateAsync_sets_normalized_email_and_security_stamp()
    {
        using var host = new IdentityTestHost();

        var result = await host.UserManager.CreateAsync(NewUser("Mixed.Case@Example.com"), "Password1");

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Code)));
        var saved = Assert.Single(host.Fixture.Context.Users);
        Assert.Equal("MIXED.CASE@EXAMPLE.COM", saved.NormalizedEmail);
        Assert.False(string.IsNullOrEmpty(saved.SecurityStamp));
    }

    [Fact]
    public async Task FindByEmailAsync_ignores_case()
    {
        using var host = new IdentityTestHost();
        await host.UserManager.CreateAsync(NewUser("Mixed.Case@Example.com"), "Password1");

        var found = await host.UserManager.FindByEmailAsync("mixed.case@example.com");

        Assert.NotNull(found);
        Assert.Equal("Mixed.Case@Example.com", found.Email);
    }

    [Fact]
    public async Task CheckPasswordAsync_accepts_the_right_password_only()
    {
        using var host = new IdentityTestHost();
        var user = NewUser("a@example.com");
        await host.UserManager.CreateAsync(user, "Password1");

        Assert.True(await host.UserManager.CheckPasswordAsync(user, "Password1"));
        Assert.False(await host.UserManager.CheckPasswordAsync(user, "wrong-password"));
    }

    [Fact]
    public async Task CreateAsync_rejects_password_shorter_than_8()
    {
        using var host = new IdentityTestHost();

        var result = await host.UserManager.CreateAsync(NewUser("a@example.com"), "short1");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordTooShort");
        Assert.Empty(host.Fixture.Context.Users);
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account_and_the_count_is_persisted()
    {
        using var host = new IdentityTestHost();
        var user = NewUser("a@example.com");
        await host.UserManager.CreateAsync(user, "Password1");

        for (var i = 0; i < 5; i++)
            await host.UserManager.AccessFailedAsync(user);

        Assert.True(await host.UserManager.IsLockedOutAsync(user));
        host.Fixture.Context.ChangeTracker.Clear();
        var reloaded = Assert.Single(host.Fixture.Context.Users);
        Assert.NotNull(reloaded.LockoutEnd);
        Assert.True(reloaded.LockoutEnd > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Principal_carries_role_and_full_name()
    {
        using var host = new IdentityTestHost();
        var user = NewUser("staff@example.com", UserRole.Employee);
        await host.UserManager.CreateAsync(user, "Password1");

        var principal = await host.ClaimsFactory.CreateAsync(user);

        Assert.True(principal.IsInRole("Employee"));
        Assert.False(principal.IsInRole("Admin"));
        Assert.Equal("Test User", principal.FindFirstValue(AppUserClaimsPrincipalFactory.FullNameClaim));
        Assert.Equal("staff@example.com", principal.Identity?.Name);
    }

    private static User NewUser(string email, UserRole role = UserRole.Customer) => new()
    {
        Email = email,
        FullName = "Test User",
        Role = role,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
