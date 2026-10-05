using System.Security.Cryptography;

namespace AppleStore.Infrastructure.Services;

public class OtpService : IOtpService
{
    // How long any emailed code (registration, password reset) stays valid.
    public static readonly TimeSpan Validity = TimeSpan.FromMinutes(5);

    public string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public bool IsValid(string providedCode, string expectedCode, DateTime expiresAtUtc, DateTime nowUtc) =>
        nowUtc < expiresAtUtc && string.Equals(providedCode, expectedCode, StringComparison.Ordinal);
}
