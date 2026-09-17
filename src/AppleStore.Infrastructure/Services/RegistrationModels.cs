namespace AppleStore.Infrastructure.Services;

public record RegisterRequest(string Email, string Password, string FullName, string? Phone);

public enum RegistrationError
{
    EmailAlreadyUsed,
    PhoneAlreadyUsed,
    AttemptNotFound,
    InvalidOtp,
}

public record RegistrationStartResult(bool Success, string? AttemptId, RegistrationError? Error);

public record RegistrationConfirmResult(bool Success, int? UserId, RegistrationError? Error);
