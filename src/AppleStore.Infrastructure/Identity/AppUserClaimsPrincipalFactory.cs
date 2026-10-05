using System.Security.Claims;
using AppleStore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppleStore.Infrastructure.Identity;

// Builds the signed-in user's cookie identity. Identity adds the id, email
// and security stamp; this adds the role (so [Authorize(Roles = "Admin")]
// and User.IsInRole work) and the full name shown in the nav.
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
{
    public const string FullNameClaim = "full_name";

    public AppUserClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
        : base(userManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, user.Role.ToString()));
        identity.AddClaim(new Claim(FullNameClaim, user.FullName));
        return identity;
    }
}
