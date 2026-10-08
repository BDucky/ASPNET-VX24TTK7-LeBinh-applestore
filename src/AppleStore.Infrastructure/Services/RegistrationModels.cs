namespace AppleStore.Infrastructure.Services;

public record RegisterRequest(string Email, string Password, string FullName, string? Phone);

public enum RegistrationError
{
    EmailAlreadyUsed,
    PhoneAlreadyUsed,
    AttemptNotFound,
    InvalidOtp,
    PasswordTooWeak,
    EmailSendFailed,
    // Five wrong codes: the attempt is dropped and the visitor registers again.
    TooManyAttempts,
}

public record RegistrationStartResult(bool Success, string? AttemptId, RegistrationError? Error);

public record RegistrationConfirmResult(bool Success, int? UserId, RegistrationError? Error);
