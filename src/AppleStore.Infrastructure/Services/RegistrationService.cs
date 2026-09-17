using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AppleStore.Infrastructure.Services;

public class RegistrationService : IRegistrationService
{
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IOtpService _otp;
    private readonly IEmailSender _emailSender;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public RegistrationService(AppDbContext db, IMemoryCache cache, IOtpService otp, IEmailSender emailSender)
    {
        _db = db;
        _cache = cache;
        _otp = otp;
        _emailSender = emailSender;
    }

    public async Task<RegistrationStartResult> StartAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email, ct))
            return new RegistrationStartResult(false, null, RegistrationError.EmailAlreadyUsed);

        if (!string.IsNullOrWhiteSpace(request.Phone) &&
            await _db.Users.AnyAsync(u => u.Phone == request.Phone, ct))
            return new RegistrationStartResult(false, null, RegistrationError.PhoneAlreadyUsed);

        var attemptId = Guid.NewGuid().ToString("N");
        var code = _otp.GenerateCode();
        var expiresAtUtc = DateTime.UtcNow.Add(OtpValidity);

        // Hash immediately; the plaintext password never sits in the cache.
        var passwordHash = _passwordHasher.HashPassword(null!, request.Password);

        var pending = new PendingRegistration(request.Email, request.Phone, request.FullName, passwordHash, code, expiresAtUtc);
        _cache.Set(CacheKey(attemptId), pending, expiresAtUtc);

        await _emailSender.SendAsync(
            request.Email,
            "Your Apple Store verification code",
            $"Your code is {code}. It expires in 5 minutes.",
            ct);

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

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _cache.Remove(CacheKey(attemptId));

        return new RegistrationConfirmResult(true, user.Id, null);
    }

    private static string CacheKey(string attemptId) => $"registration-attempt:{attemptId}";

    private sealed record PendingRegistration(
        string Email,
        string? Phone,
        string FullName,
        string PasswordHash,
        string OtpCode,
        DateTime ExpiresAtUtc);
}
