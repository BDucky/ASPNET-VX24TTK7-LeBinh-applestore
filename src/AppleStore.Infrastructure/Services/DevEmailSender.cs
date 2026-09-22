using Microsoft.Extensions.Logging;

namespace AppleStore.Infrastructure.Services;

// Local-dev email sender: logs the message instead of sending it, so
// registration/OTP/order-confirmation flows work with zero external account
// setup, matching the SQLite-now/real-provider-later pattern already used for
// the database (see docs/architecture.md). Swap for a real provider (SendGrid,
// SMTP) before deploying; nothing else in the codebase needs to change since
// callers depend on IEmailSender, not this class.
public class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("DEV EMAIL to {ToEmail} | {Subject}\n{Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
