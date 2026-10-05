using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

public static class UserQueryExtensions
{
    // The one rule for "this phone already belongs to an account", used by
    // registration and by the profile page (which passes its own id so a
    // user keeping their number is not refused).
    public static Task<bool> PhoneTakenAsync(this IQueryable<User> users, string phone, int? exceptUserId = null, CancellationToken ct = default) =>
        users.AnyAsync(u => u.Phone == phone && (exceptUserId == null || u.Id != exceptUserId), ct);
}
