using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppleStore.Infrastructure.Payments;

public static class PaymentServiceCollectionExtensions
{
    public const string ModeKey = "Payments:Mode";

    // The one place that decides which online gateway exists. The "Payments"
    // section is bound with the options pattern and checked when the app
    // starts (after every configuration source is loaded): an unknown mode is
    // a typo, and the simulated gateway, which takes payments without
    // charging anyone, is refused outside Development. Either stops the app.
    public static IServiceCollection AddAppleStorePayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.Section))
            .Validate(o => string.IsNullOrWhiteSpace(o.Mode) || o.IsSimulated,
                $"{ModeKey} is not known. Use \"Simulated\", or leave it empty for cash on delivery only.")
            .Validate<IHostEnvironment>((o, environment) => !o.IsSimulated || environment.IsDevelopment(),
                $"{ModeKey}=Simulated takes payments without charging anyone and is refused outside Development.")
            .ValidateOnStart();

        services.AddSingleton<SimulatedPaymentGateway>();
        services.AddScoped<IPaymentService>(sp => new PaymentService(
            sp.GetRequiredService<Data.AppDbContext>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<PaymentService>>(),
            sp.GetRequiredService<IOptions<PaymentOptions>>().Value.IsSimulated ? sp.GetRequiredService<SimulatedPaymentGateway>() : null));
        return services;
    }
}
