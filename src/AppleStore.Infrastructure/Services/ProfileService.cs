using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _db;

    public ProfileService(AppDbContext db) => _db = db;

    public async Task<ProfileResult> UpdateAsync(int userId, ProfileUpdate update, CancellationToken ct = default)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return new ProfileResult(false, ProfileError.UserNotFound);

        var phone = string.IsNullOrWhiteSpace(update.Phone) ? null : update.Phone.Trim();
        if (phone is not null && await _db.Users.PhoneTakenAsync(phone, userId, ct))
            return new ProfileResult(false, ProfileError.PhoneAlreadyUsed);

        user.FullName = update.FullName.Trim();
        user.Phone = phone;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new ProfileResult(true, null);
    }

    public async Task<IReadOnlyList<Address>> GetAddressesAsync(int userId, CancellationToken ct = default) =>
        await _db.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

    public Task<Address?> GetAddressAsync(int userId, int addressId, CancellationToken ct = default) =>
        _db.Addresses.AsNoTracking().SingleOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct);

    public async Task<Address> AddAddressAsync(int userId, AddressInput input, CancellationToken ct = default)
    {
        var addresses = await OwnedAsync(userId, ct);
        var address = new Address { UserId = userId };
        Apply(address, input);
        _db.Addresses.Add(address);
        // The first address is the default even if the box was not ticked,
        // so checkout always has one to start from.
        if (input.IsDefault || addresses.Count == 0)
            MakeDefault(addresses, address);
        await _db.SaveChangesAsync(ct);
        return address;
    }

    public async Task<bool> UpdateAddressAsync(int userId, int addressId, AddressInput input, CancellationToken ct = default)
    {
        var addresses = await OwnedAsync(userId, ct);
        var address = addresses.SingleOrDefault(a => a.Id == addressId);
        if (address is null)
            return false;

        Apply(address, input);
        // Unticking the box keeps it the default; the user makes another
        // address default instead, so there is never none.
        if (input.IsDefault)
            MakeDefault(addresses, address);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAddressAsync(int userId, int addressId, CancellationToken ct = default)
    {
        var address = (await OwnedAsync(userId, ct)).SingleOrDefault(a => a.Id == addressId);
        if (address is null)
            return false;

        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetDefaultAddressAsync(int userId, int addressId, CancellationToken ct = default)
    {
        var addresses = await OwnedAsync(userId, ct);
        var address = addresses.SingleOrDefault(a => a.Id == addressId);
        if (address is null)
            return false;

        MakeDefault(addresses, address);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Loads (tracked) every address the user owns; ownership is enforced by
    // only ever looking an address up inside this list.
    private Task<List<Address>> OwnedAsync(int userId, CancellationToken ct) =>
        _db.Addresses.Where(a => a.UserId == userId).ToListAsync(ct);

    private static void MakeDefault(IEnumerable<Address> addresses, Address chosen)
    {
        foreach (var other in addresses)
            other.IsDefault = false;
        chosen.IsDefault = true;
    }

    private static void Apply(Address address, AddressInput input)
    {
        address.Label = Blank(input.Label);
        address.FullName = input.FullName.Trim();
        address.Phone = input.Phone.Trim();
        address.AddressLine = input.AddressLine.Trim();
        address.Ward = Blank(input.Ward);
        address.District = Blank(input.District);
        address.City = Blank(input.City);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
