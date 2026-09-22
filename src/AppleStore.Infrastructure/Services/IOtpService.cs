namespace AppleStore.Infrastructure.Services;

public interface IOtpService
{
    string GenerateCode();

    bool IsValid(string providedCode, string expectedCode, DateTime expiresAtUtc, DateTime nowUtc);
}
