using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otp;
    private readonly IEmailSender _emailSender;

    public PasswordResetService(AppDbContext db, UserManager<User> userManager, IOtpService otp, IEmailSender emailSender)
    {
        _db = db;
        _userManager = userManager;
        _otp = otp;
        _emailSender = emailSender;
    }

    public async Task<PasswordResetStartResult> StartAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return new PasswordResetStartResult(false, PasswordResetError.NoAccount);

        var now = DateTime.UtcNow;

        // Only the newest code works: retire any earlier unused one.
        await _db.UserTokens
            .Where(t => t.UserId == user.Id && t.Type == UserTokenType.ResetPasswordOtp && t.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now), ct);

        var code = _otp.GenerateCode();
        _db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Type = UserTokenType.ResetPasswordOtp,
            // Stored hashed, like a password, so a database copy does not hand out live codes.
            Token = _userManager.PasswordHasher.HashPassword(user, code),
            ExpiredAt = now.Add(OtpService.Validity),
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        await _emailSender.SendAsync(
            user.Email,
            "Your Apple Store password reset code",
            $"Your code is {code}. It expires in {OtpService.Validity.TotalMinutes:0} minutes. If you did not ask to reset your password, ignore this email.",
            ct);

        return new PasswordResetStartResult(true, null);
    }

    public async Task<PasswordResetResult> ResetAsync(string email, string code, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Failed(PasswordResetError.NoAccount);

        if (await _userManager.IsLockedOutAsync(user))
            return Failed(PasswordResetError.LockedOut);

        // Checked before the code, so a typo in the new password does not
        // spend the code or count as a failed attempt.
        foreach (var validator in _userManager.PasswordValidators)
        {
            if (!(await validator.ValidateAsync(_userManager, user, newPassword)).Succeeded)
                return Failed(PasswordResetError.PasswordTooWeak);
        }

        var now = DateTime.UtcNow;
        var token = await _db.UserTokens
            .Where(t => t.UserId == user.Id && t.Type == UserTokenType.ResetPasswordOtp && t.UsedAt == null)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);
        if (token is null || token.ExpiredAt <= now)
            return Failed(PasswordResetError.CodeExpired);

        var matches = _userManager.PasswordHasher.VerifyHashedPassword(user, token.Token, code) != PasswordVerificationResult.Failed;
        if (!matches)
        {
            await _userManager.AccessFailedAsync(user);
            return Failed(await _userManager.IsLockedOutAsync(user) ? PasswordResetError.LockedOut : PasswordResetError.InvalidCode);
        }

        // Claim the code with a conditional update, so two requests racing with
        // the same code cannot both reset the password.
        var claimed = await _db.UserTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now), ct);
        if (claimed == 0)
            return Failed(PasswordResetError.CodeExpired);

        // Identity's own reset: re-validates the password, hashes it, and
        // changes the security stamp, which signs out other sessions.
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!reset.Succeeded)
            throw new InvalidOperationException("Password reset failed after the code was accepted: " +
                                                string.Join(", ", reset.Errors.Select(e => e.Code)));

        await _userManager.ResetAccessFailedCountAsync(user);
        return new PasswordResetResult(true, user.Id, null);
    }

    private static PasswordResetResult Failed(PasswordResetError error) => new(false, null, error);
}
