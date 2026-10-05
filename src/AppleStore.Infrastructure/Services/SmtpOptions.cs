using System.ComponentModel.DataAnnotations;

namespace AppleStore.Infrastructure.Services;

// The "Smtp" configuration section. Host empty means no real mail server is
// configured and DevEmailSender (log only) is used. The password belongs in
// user-secrets or an environment variable, never in appsettings.json.
public class SmtpOptions
{
    public const string Section = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Apple Store";

    // Whether TLS also asks if Gmail's certificate was revoked. On by default.
    // .NET on macOS can report that check as "incomplete" and MailKit then
    // refuses the connection; a developer may turn it off for their machine
    // (user-secrets). The certificate chain and host name are still checked.
    public bool CheckCertificateRevocation { get; set; } = true;
}
