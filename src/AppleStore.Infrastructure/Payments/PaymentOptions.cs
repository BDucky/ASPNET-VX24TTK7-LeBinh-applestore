namespace AppleStore.Infrastructure.Payments;

// The "Payments" section. Mode empty: cash on delivery only. "Simulated":
// the in-app gateway (Development only, see PaymentServiceCollectionExtensions).
public sealed class PaymentOptions
{
    public const string Section = "Payments";

    public string? Mode { get; set; }

    public bool IsSimulated => string.Equals(Mode, "Simulated", StringComparison.OrdinalIgnoreCase);
}
