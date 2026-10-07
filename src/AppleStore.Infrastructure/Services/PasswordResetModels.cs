namespace AppleStore.Infrastructure.Services;

public enum PasswordResetError
{
    NoAccount,
    InvalidCode,
    CodeExpired,
    LockedOut,
    PasswordTooWeak,
    EmailSendFailed,
}

public record PasswordResetStartResult(bool Success, PasswordResetError? Error);

public record PasswordResetResult(bool Success, int? UserId, PasswordResetError? Error);
