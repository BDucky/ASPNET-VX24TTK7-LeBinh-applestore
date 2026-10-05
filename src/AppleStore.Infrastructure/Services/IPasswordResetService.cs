namespace AppleStore.Infrastructure.Services;

// Use case 4: forgot password by a 6-digit code sent to the account's email.
// The code is stored hashed in UserTokens (Type = ResetPasswordOtp), works
// once, and expires with the same validity as the registration code. Wrong
// codes count toward Identity's lockout.
public interface IPasswordResetService
{
    Task<PasswordResetStartResult> StartAsync(string email, CancellationToken ct = default);

    Task<PasswordResetResult> ResetAsync(string email, string code, string newPassword, CancellationToken ct = default);
}
