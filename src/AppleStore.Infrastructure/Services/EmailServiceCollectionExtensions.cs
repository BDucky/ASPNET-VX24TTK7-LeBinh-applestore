using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Infrastructure.Services;

public static class EmailServiceCollectionExtensions
{
    // RED stub: always the log-only sender.
    public static IServiceCollection AddAppleStoreEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEmailSender, DevEmailSender>();
        return services;
    }
}
