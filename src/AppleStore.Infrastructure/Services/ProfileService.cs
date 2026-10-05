using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

// RED stub.
public class ProfileService : IProfileService
{
    public ProfileService(AppDbContext db)
    {
    }

    public Task<ProfileResult> UpdateAsync(int userId, ProfileUpdate update, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Address>> GetAddressesAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Address?> GetAddressAsync(int userId, int addressId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Address> AddAddressAsync(int userId, AddressInput input, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> UpdateAddressAsync(int userId, int addressId, AddressInput input, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> DeleteAddressAsync(int userId, int addressId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> SetDefaultAddressAsync(int userId, int addressId, CancellationToken ct = default) => throw new NotImplementedException();
}
