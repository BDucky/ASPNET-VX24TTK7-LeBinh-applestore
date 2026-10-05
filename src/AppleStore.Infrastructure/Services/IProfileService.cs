using AppleStore.Domain.Entities;

namespace AppleStore.Infrastructure.Services;

// Use case 6: the signed-in user's own details and delivery addresses. Every
// address method takes the owner's id, so one user can never read or change
// another user's address.
public interface IProfileService
{
    Task<ProfileResult> UpdateAsync(int userId, ProfileUpdate update, CancellationToken ct = default);

    Task<IReadOnlyList<Address>> GetAddressesAsync(int userId, CancellationToken ct = default);

    Task<Address?> GetAddressAsync(int userId, int addressId, CancellationToken ct = default);

    Task<Address> AddAddressAsync(int userId, AddressInput input, CancellationToken ct = default);

    Task<bool> UpdateAddressAsync(int userId, int addressId, AddressInput input, CancellationToken ct = default);

    Task<bool> DeleteAddressAsync(int userId, int addressId, CancellationToken ct = default);

    Task<bool> SetDefaultAddressAsync(int userId, int addressId, CancellationToken ct = default);
}
