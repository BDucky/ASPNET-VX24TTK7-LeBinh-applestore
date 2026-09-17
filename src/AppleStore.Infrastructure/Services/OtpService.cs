namespace AppleStore.Infrastructure.Services;

public class OtpService : IOtpService
{
    public string GenerateCode() => throw new NotImplementedException();

    public bool IsValid(string providedCode, string expectedCode, DateTime expiresAtUtc, DateTime nowUtc) =>
        throw new NotImplementedException();
}
