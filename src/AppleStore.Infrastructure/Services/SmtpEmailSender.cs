using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AppleStore.Infrastructure.Services;

// RED stub.
public class SmtpEmailSender : IEmailSender
{
    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
    }

    public static MimeMessage BuildMessage(SmtpOptions options, string toEmail, string subject, string body) =>
        throw new NotImplementedException();

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
