namespace AppleStore.Domain.Enums;

// Inferred from the use-case narrative (OTP for registration, OTP for password
// reset); the schema's Type column has no explicit enumerated domain.
public enum UserTokenType
{
    RegisterOtp = 0,
    ResetPasswordOtp = 1,
}
