using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Tests;

public class ProfileServiceTests
{
    private static async Task<(ProfileService Sut, IdentityTestHost Host, User Me, User Other)> CreateSut()
    {
        var host = new IdentityTestHost();
        var me = await AddUser(host, "me@example.com", "0900000001");
        var other = await AddUser(host, "other@example.com", "0900000002");
        return (new ProfileService(host.Fixture.Context), host, me, other);
    }

    private static async Task<User> AddUser(IdentityTestHost host, string email, string phone)
    {
        var user = new User { Email = email, FullName = "Name", Phone = phone, Role = UserRole.Customer, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Assert.True((await host.UserManager.CreateAsync(user, "Password1")).Succeeded);
        return user;
    }

    private static AddressInput Input(string line = "1 Le Loi", bool isDefault = false) =>
        new("Home", "Nguyen Van A", "0911111111", line, "Ben Nghe", "District 1", "Ho Chi Minh City", isDefault);

    private static async Task<List<Address>> Saved(IdentityTestHost host, int userId) =>
        await host.Fixture.Context.Addresses.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.Id).ToListAsync();

    [Fact]
    public async Task UpdateAsync_saves_name_and_phone()
    {
        var (sut, host, me, _) = await CreateSut();
        using var _ = host;

        var result = await sut.UpdateAsync(me.Id, new ProfileUpdate("New Name", "0900000009"));

        Assert.True(result.Success);
        var saved = await host.Fixture.Context.Users.AsNoTracking().SingleAsync(u => u.Id == me.Id);
        Assert.Equal("New Name", saved.FullName);
        Assert.Equal("0900000009", saved.Phone);
    }

    [Fact]
    public async Task UpdateAsync_refuses_a_phone_another_account_uses_but_accepts_its_own()
    {
        var (sut, host, me, _) = await CreateSut();
        using var _ = host;

        var taken = await sut.UpdateAsync(me.Id, new ProfileUpdate("Name", "0900000002"));
        var own = await sut.UpdateAsync(me.Id, new ProfileUpdate("Name", "0900000001"));

        Assert.Equal(ProfileError.PhoneAlreadyUsed, taken.Error);
        Assert.True(own.Success);
    }

    [Fact]
    public async Task The_first_address_becomes_the_default()
    {
        var (sut, host, me, _) = await CreateSut();
        using var _ = host;

        await sut.AddAddressAsync(me.Id, Input());

        Assert.True(Assert.Single(await Saved(host, me.Id)).IsDefault);
    }

    [Fact]
    public async Task Only_one_address_is_default_at_a_time()
    {
        var (sut, host, me, _) = await CreateSut();
        using var _ = host;
        var first = await sut.AddAddressAsync(me.Id, Input("1 Le Loi"));
        var second = await sut.AddAddressAsync(me.Id, Input("2 Le Loi", isDefault: true));

        Assert.Equal([second.Id], (await Saved(host, me.Id)).Where(a => a.IsDefault).Select(a => a.Id));

        await sut.SetDefaultAddressAsync(me.Id, first.Id);
        Assert.Equal([first.Id], (await Saved(host, me.Id)).Where(a => a.IsDefault).Select(a => a.Id));

        await sut.UpdateAddressAsync(me.Id, second.Id, Input("2 Le Loi, floor 3", isDefault: true));
        Assert.Equal([second.Id], (await Saved(host, me.Id)).Where(a => a.IsDefault).Select(a => a.Id));
    }

    [Fact]
    public async Task Another_users_address_cannot_be_read_changed_or_deleted()
    {
        var (sut, host, me, other) = await CreateSut();
        using var _ = host;
        var theirs = await sut.AddAddressAsync(other.Id, Input("Theirs"));

        Assert.Null(await sut.GetAddressAsync(me.Id, theirs.Id));
        Assert.False(await sut.UpdateAddressAsync(me.Id, theirs.Id, Input("Mine now")));
        Assert.False(await sut.SetDefaultAddressAsync(me.Id, theirs.Id));
        Assert.False(await sut.DeleteAddressAsync(me.Id, theirs.Id));
        Assert.Empty(await sut.GetAddressesAsync(me.Id));
        Assert.Equal("Theirs", Assert.Single(await Saved(host, other.Id)).AddressLine);
    }

    [Fact]
    public async Task Update_and_delete_work_on_the_users_own_address()
    {
        var (sut, host, me, _) = await CreateSut();
        using var _ = host;
        var mine = await sut.AddAddressAsync(me.Id, Input("Old line"));

        Assert.True(await sut.UpdateAddressAsync(me.Id, mine.Id, Input("New line")));
        Assert.Equal("New line", Assert.Single(await Saved(host, me.Id)).AddressLine);

        Assert.True(await sut.DeleteAddressAsync(me.Id, mine.Id));
        Assert.Empty(await Saved(host, me.Id));
    }
}
