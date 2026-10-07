namespace AppleStore.Infrastructure.Identity;

// The "SeedAdmin" section: the first admin account, created at startup when
// no admin exists yet. Set through user-secrets on a developer machine or
// SEEDADMIN__EMAIL / SEEDADMIN__PASSWORD environment variables elsewhere;
// never committed (decided 2026-10-07).
public sealed class SeedAdminOptions
{
    public const string Section = "SeedAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string FullName { get; set; } = "Administrator";
}
