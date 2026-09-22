namespace AppleStore.Infrastructure.Services;

public interface IRegistrationService
{
    // Validates the submitted info and, if accepted, emails an OTP. Does NOT
    // create a User row yet: the report's own use case says the account is
    // created only after OTP confirmation, so nothing is persisted to the
    // database at this step (see docs/data-model.md, "pending registration"
    // gap). The pending data lives behind attemptId until ConfirmAsync or
    // expiry, whichever comes first.
    Task<RegistrationStartResult> StartAsync(RegisterRequest request, CancellationToken ct = default);

    Task<RegistrationConfirmResult> ConfirmAsync(string attemptId, string otpCode, CancellationToken ct = default);
}
