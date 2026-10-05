namespace AppleStore.Infrastructure.Services;

public record ProfileUpdate(string FullName, string? Phone);

public enum ProfileError
{
    UserNotFound,
    PhoneAlreadyUsed,
}

public record ProfileResult(bool Success, ProfileError? Error);

public record AddressInput(
    string? Label,
    string FullName,
    string Phone,
    string AddressLine,
    string? Ward,
    string? District,
    string? City,
    bool IsDefault);
