using System.Security.Claims;

namespace AppleStore.Web.Controllers;

public static class StaffIdentity
{
    // The signed-in account's id (Identity's name identifier claim), the one
    // way the staff pages read who made a change.
    public static int? AccountId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
