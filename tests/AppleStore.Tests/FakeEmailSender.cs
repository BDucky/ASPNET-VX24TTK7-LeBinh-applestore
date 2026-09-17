using AppleStore.Infrastructure.Services;

namespace AppleStore.Tests;

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        Sent.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}
