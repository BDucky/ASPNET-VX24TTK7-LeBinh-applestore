using AppleStore.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace AppleStore.Infrastructure.Services;

public class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IOtpService _otp;
    private readonly IEmailSender _emailSender;

    public RegistrationService(AppDbContext db, IMemoryCache cache, IOtpService otp, IEmailSender emailSender)
    {
        _db = db;
        _cache = cache;
        _otp = otp;
        _emailSender = emailSender;
    }

    public Task<RegistrationStartResult> StartAsync(RegisterRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<RegistrationConfirmResult> ConfirmAsync(string attemptId, string otpCode, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
