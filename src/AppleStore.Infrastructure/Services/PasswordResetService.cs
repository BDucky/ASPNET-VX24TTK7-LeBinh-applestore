using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace AppleStore.Infrastructure.Services;

// RED stub.
public class PasswordResetService : IPasswordResetService
{
    public PasswordResetService(AppDbContext db, UserManager<User> userManager, IOtpService otp, IEmailSender emailSender)
    {
    }

    public Task<PasswordResetStartResult> StartAsync(string email, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PasswordResetResult> ResetAsync(string email, string code, string newPassword, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
