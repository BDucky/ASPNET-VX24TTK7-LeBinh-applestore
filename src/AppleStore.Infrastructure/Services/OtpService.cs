using System.Security.Cryptography;

namespace AppleStore.Infrastructure.Services;

public class OtpService : IOtpService
{
    public string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public bool IsValid(string providedCode, string expectedCode, DateTime expiresAtUtc, DateTime nowUtc) =>
        nowUtc < expiresAtUtc && string.Equals(providedCode, expectedCode, StringComparison.Ordinal);
}
