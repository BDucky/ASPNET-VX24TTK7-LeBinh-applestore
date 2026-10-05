using AppleStore.Infrastructure.Services;

namespace AppleStore.Tests;

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    // When true, behaves like a mail server that cannot be reached.
    public bool Fail { get; set; }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        if (Fail)
            throw new EmailSendException("Test: mail server unreachable");
        Sent.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}
