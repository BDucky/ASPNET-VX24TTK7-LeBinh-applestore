using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Infrastructure.Services;

public static class EmailServiceCollectionExtensions
{
    // The one place that decides how mail leaves the app. With Smtp:Host set,
    // the whole "Smtp" section is validated when the app starts (so a half
    // configured server fails at startup, not at the first registration) and
    // SmtpEmailSender is used. Without it, DevEmailSender writes mail to the log.
    public static IServiceCollection AddAppleStoreEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SmtpOptions.Section);
        if (string.IsNullOrWhiteSpace(section[nameof(SmtpOptions.Host)]))
        {
            services.AddScoped<IEmailSender, DevEmailSender>();
            return services;
        }

        services.AddOptions<SmtpOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
