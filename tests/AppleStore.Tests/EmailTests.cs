using System.Diagnostics;
using AppleStore.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AppleStore.Tests;

public class EmailTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppleStoreEmail(config);
        return services.BuildServiceProvider();
    }

    private static readonly Dictionary<string, string?> FullSmtp = new()
    {
        ["Smtp:Host"] = "smtp.example.com",
        ["Smtp:Port"] = "587",
        ["Smtp:UserName"] = "store@example.com",
        ["Smtp:Password"] = "app-password",
        ["Smtp:FromAddress"] = "store@example.com",
    };

    [Fact]
    public void Without_smtp_settings_mail_is_only_logged()
    {
        using var provider = Build(new());
        using var scope = provider.CreateScope();

        Assert.IsType<DevEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void With_smtp_settings_mail_goes_through_smtp()
    {
        using var provider = Build(FullSmtp);
        using var scope = provider.CreateScope();

        Assert.IsType<SmtpEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void A_host_without_the_rest_of_the_settings_is_refused()
    {
        using var provider = Build(new() { ["Smtp:Host"] = "smtp.example.com" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
    }

    [Fact]
    public void Message_has_sender_receiver_subject_and_plain_text_body()
    {
        var options = new SmtpOptions { FromAddress = "store@example.com", FromName = "Apple Store" };

        var message = SmtpEmailSender.BuildMessage(options, "buyer@example.com", "Your code", "Your code is 123456.");

        Assert.Equal("\"Apple Store\" <store@example.com>", message.From.ToString());
        Assert.Equal("buyer@example.com", message.To.ToString());
        Assert.Equal("Your code", message.Subject);
        Assert.Equal("Your code is 123456.", message.TextBody?.Trim());
    }

    [Fact]
    public async Task An_unreachable_server_becomes_an_EmailSendException()
    {
        // Real socket to a closed local port: no mock between the sender and the failure.
        var options = Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = 1,
            UserName = "u",
            Password = "p",
            FromAddress = "store@example.com",
        });
        var sender = new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance);
        var clock = Stopwatch.StartNew();

        await Assert.ThrowsAsync<EmailSendException>(() => sender.SendAsync("buyer@example.com", "s", "b"));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"took {clock.Elapsed}");
    }

    [Fact]
    public async Task Registration_reports_a_failed_email_and_keeps_no_attempt()
    {
        using var host = new IdentityTestHost();
        var mail = new FakeEmailSender { Fail = true };
        var sut = new RegistrationService(host.Fixture.Context, new MemoryCache(new MemoryCacheOptions()), new OtpService(), mail, host.UserManager);

        var result = await sut.StartAsync(new RegisterRequest("new@example.com", "Password1", "New", null));

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.EmailSendFailed, result.Error);
        Assert.Null(result.AttemptId);
    }

    [Fact]
    public async Task Password_reset_reports_a_failed_email_and_leaves_no_live_code()
    {
        using var host = new IdentityTestHost();
        var user = new AppleStore.Domain.Entities.User { Email = "user@example.com", FullName = "U", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await host.UserManager.CreateAsync(user, "Password1");
        var mail = new FakeEmailSender { Fail = true };
        var sut = new PasswordResetService(host.Fixture.Context, host.UserManager, new OtpService(), mail);

        var result = await sut.StartAsync("user@example.com");

        Assert.Equal(PasswordResetError.EmailSendFailed, result.Error);
        Assert.DoesNotContain(host.Fixture.Context.UserTokens.ToList(), t => t.UsedAt == null);
    }
}
