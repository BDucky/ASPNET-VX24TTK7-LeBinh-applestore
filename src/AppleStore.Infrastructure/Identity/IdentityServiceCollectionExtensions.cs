using AppleStore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    // The one place Identity's rules for this app are set, shared by the web
    // app and the tests so both run the same policy.
    public static IdentityBuilder AddAppleStoreIdentityCore(this IServiceCollection services)
    {
        // Signs the one-off token PasswordResetService hands to ResetPasswordAsync.
        services.AddDataProtection();
        return services.AddIdentityCore<User>(options =>
            {
                // Agreed 2026-10-05: at least 8 characters, no other complexity rule.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                // Lockout: Identity's own defaults (5 failed attempts, 5 minutes),
                // agreed 2026-10-05 because the report asks for a limit but gives no number.
                options.User.RequireUniqueEmail = true;
                // The user name is the email, already checked by the register
                // form; Identity's default character list would reject some
                // valid addresses only after the OTP step. Empty means any character.
                options.User.AllowedUserNameCharacters = string.Empty;
            })
            .AddUserStore<UserStore>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
    }
}
