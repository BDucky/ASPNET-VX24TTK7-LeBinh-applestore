using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AppleStore.Infrastructure.Services;

// Sends mail through a real SMTP server (Gmail with an App Password, or any
// provider) using MailKit. Any failure to connect, authenticate or send is
// logged and rethrown as EmailSendException, so callers can show the visitor
// a message instead of an error page. MailKit's own timeout (2 minutes)
// bounds every wait.
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public static MimeMessage BuildMessage(SmtpOptions options, string toEmail, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        var message = BuildMessage(_options, toEmail, subject, body);
        // 465 is TLS from the first byte; anything else (587) must upgrade with
        // STARTTLS, and the connection fails rather than send the password in clear.
        var security = _options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, security, ct);
            await client.AuthenticateAsync(_options.UserName, _options.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sending \"{Subject}\" to {ToEmail} through {Host}:{Port} failed", subject, toEmail, _options.Host, _options.Port);
            throw new EmailSendException("The email could not be sent.", ex);
        }
    }
}
