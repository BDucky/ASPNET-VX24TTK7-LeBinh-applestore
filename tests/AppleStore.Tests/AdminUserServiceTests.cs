using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;

namespace AppleStore.Tests;

// Role changes through the real UserManager and UserStore.
public sealed class AdminUserServiceTests : IDisposable
{
    private readonly IdentityTestHost _host = new();
    private readonly AdminUserService _sut;

    public AdminUserServiceTests()
    {
        _sut = new AdminUserService(_host.Fixture.Context, _host.UserManager);
    }

    public void Dispose() => _host.Dispose();

    private async Task<User> UserAsync(string email, string name, UserRole role = UserRole.Customer)
    {
        var user = new User { Email = email, FullName = name, Role = role, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Assert.True((await _host.UserManager.CreateAsync(user, "Password1")).Succeeded);
        return user;
    }

    [Fact]
    public async Task Giving_a_role_changes_it_and_the_security_stamp()
    {
        var admin = await UserAsync("admin@example.com", "Admin", UserRole.Admin);
        var staff = await UserAsync("staff@example.com", "Staff Person");
        var stamp = staff.SecurityStamp;

        var outcome = await _sut.ChangeRoleAsync(admin.Id, staff.Id, UserRole.Employee);

        Assert.Equal(RoleChangeOutcome.Done, outcome);
        var after = await _host.UserManager.FindByIdAsync(staff.Id.ToString());
        Assert.Equal(UserRole.Employee, after!.Role);
        Assert.NotEqual(stamp, after.SecurityStamp);
    }

    [Fact]
    public async Task An_admin_cannot_change_their_own_role()
    {
        var admin = await UserAsync("admin@example.com", "Admin", UserRole.Admin);

        Assert.Equal(RoleChangeOutcome.OwnAccount, await _sut.ChangeRoleAsync(admin.Id, admin.Id, UserRole.Customer));
        Assert.Equal(UserRole.Admin, (await _host.UserManager.FindByIdAsync(admin.Id.ToString()))!.Role);
    }

    [Fact]
    public async Task A_role_that_does_not_exist_is_refused()
    {
        var admin = await UserAsync("admin@example.com", "Admin", UserRole.Admin);
        var person = await UserAsync("person@example.com", "Person");

        Assert.Equal(RoleChangeOutcome.InvalidRole, await _sut.ChangeRoleAsync(admin.Id, person.Id, (UserRole)99));
        Assert.Equal(UserRole.Customer, (await _host.UserManager.FindByIdAsync(person.Id.ToString()))!.Role);
    }

    [Fact]
    public async Task An_unknown_account_is_not_found()
    {
        var admin = await UserAsync("admin@example.com", "Admin", UserRole.Admin);

        Assert.Equal(RoleChangeOutcome.NotFound, await _sut.ChangeRoleAsync(admin.Id, 999_999, UserRole.Employee));
    }

    [Fact]
    public async Task The_list_finds_accounts_by_email_or_name_without_regard_to_case()
    {
        await UserAsync("alice@example.com", "Alice Nguyen");
        await UserAsync("bob@example.com", "Bob Tran");

        Assert.Equal(["alice@example.com", "bob@example.com"], (await _sut.ListAsync(null)).Select(u => u.Email).Order());
        Assert.Equal(["alice@example.com"], (await _sut.ListAsync("NGUYEN")).Select(u => u.Email));
        Assert.Equal(["bob@example.com"], (await _sut.ListAsync("Bob@")).Select(u => u.Email));
    }
}
