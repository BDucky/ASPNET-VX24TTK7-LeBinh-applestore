using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AppleStore.Infrastructure.Services;

public class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IOtpService _otp;
    private readonly IEmailSender _emailSender;

    private readonly UserManager<User> _userManager;

    public RegistrationService(AppDbContext db, IMemoryCache cache, IOtpService otp, IEmailSender emailSender, UserManager<User> userManager)
    {
        _userManager = userManager;
        _db = db;
        _cache = cache;
        _otp = otp;
        _emailSender = emailSender;
    }

    public async Task<RegistrationStartResult> StartAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Identity's lookup is case-insensitive, so "A@x.com" and "a@x.com" are one account.
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            return new RegistrationStartResult(false, null, RegistrationError.EmailAlreadyUsed);

        if (!string.IsNullOrWhiteSpace(request.Phone) &&
            await _db.Users.PhoneTakenAsync(request.Phone, ct: ct))
            return new RegistrationStartResult(false, null, RegistrationError.PhoneAlreadyUsed);

        // Identity's password rules (AddAppleStoreIdentityCore) are checked here,
        // before the OTP is sent, not only when the account is finally created.
        foreach (var validator in _userManager.PasswordValidators)
        {
            var check = await validator.ValidateAsync(_userManager, null!, request.Password);
            if (!check.Succeeded)
                return new RegistrationStartResult(false, null, RegistrationError.PasswordTooWeak);
        }

        var attemptId = Guid.NewGuid().ToString("N");
        var code = _otp.GenerateCode();
        var expiresAtUtc = DateTime.UtcNow.Add(OtpService.Validity);

        // Hash immediately; the plaintext password never sits in the cache.
        var passwordHash = _userManager.PasswordHasher.HashPassword(null!, request.Password);

        var pending = new PendingRegistration(request.Email, request.Phone, request.FullName, passwordHash, code, expiresAtUtc);
        _cache.Set(CacheKey(attemptId), pending, expiresAtUtc);

        try
        {
            await _emailSender.SendAsync(
                request.Email,
                "Your Apple Store verification code",
                $"Your code is {code}. It expires in {OtpService.Validity.TotalMinutes:0} minutes.",
                ct);
        }
        catch (EmailSendException)
        {
            // No code reached the visitor, so the attempt is useless; drop it.
            _cache.Remove(CacheKey(attemptId));
            return new RegistrationStartResult(false, null, RegistrationError.EmailSendFailed);
        }

        return new RegistrationStartResult(true, attemptId, null);
    }

    public async Task<RegistrationConfirmResult> ConfirmAsync(string attemptId, string otpCode, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(CacheKey(attemptId), out PendingRegistration? pending) || pending is null)
            return new RegistrationConfirmResult(false, null, RegistrationError.AttemptNotFound);

        if (!_otp.IsValid(otpCode, pending.OtpCode, pending.ExpiresAtUtc, DateTime.UtcNow))
            return new RegistrationConfirmResult(false, null, RegistrationError.InvalidOtp);

        var user = new User
        {
            Email = pending.Email,
            PasswordHash = pending.PasswordHash,
            FullName = pending.FullName,
            Phone = pending.Phone,
            Role = UserRole.Customer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // CreateAsync (not a direct insert) so Identity sets the normalized
        // email and security stamp the user needs to sign in. It also rejects
        // an email that another pending attempt has confirmed since StartAsync.
        var created = await _userManager.CreateAsync(user);
        _cache.Remove(CacheKey(attemptId));
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e => EmailTakenCodes.Contains(e.Code)))
                return new RegistrationConfirmResult(false, null, RegistrationError.EmailAlreadyUsed);
            throw new InvalidOperationException(
                "Creating the confirmed user failed: " + string.Join(", ", created.Errors.Select(e => e.Code)));
        }

        return new RegistrationConfirmResult(true, user.Id, null);
    }

    // DuplicateUserName/DuplicateEmail: Identity's validator saw the email
    // taken. DbUpdateFailed: the unique index caught a write that raced it.
    private static readonly HashSet<string> EmailTakenCodes = ["DuplicateUserName", "DuplicateEmail", "DbUpdateFailed"];

    private static string CacheKey(string attemptId) => $"registration-attempt:{attemptId}";

    private sealed record PendingRegistration(
        string Email,
        string? Phone,
        string FullName,
        string PasswordHash,
        string OtpCode,
        DateTime ExpiresAtUtc);
}
