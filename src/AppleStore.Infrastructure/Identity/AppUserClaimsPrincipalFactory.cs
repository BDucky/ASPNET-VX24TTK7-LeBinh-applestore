using System.Security.Claims;
using AppleStore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppleStore.Infrastructure.Identity;

// RED stub: adds nothing yet.
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
{
    public const string FullNameClaim = "full_name";

    public AppUserClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
        : base(userManager, options)
    {
    }

    protected override Task<ClaimsIdentity> GenerateClaimsAsync(User user) => base.GenerateClaimsAsync(user);
}
