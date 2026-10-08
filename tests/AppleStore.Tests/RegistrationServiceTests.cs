using System.Text.RegularExpressions;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;

namespace AppleStore.Tests;

public class RegistrationServiceTests
{
    // The fixture returned is the host's; disposing the host disposes it.
    private static (RegistrationService Sut, IdentityTestHost Fixture, FakeEmailSender Email) CreateSut()
    {
        var host = new IdentityTestHost();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var email = new FakeEmailSender();
        var sut = new RegistrationService(host.Fixture.Context, cache, new OtpService(), email, host.UserManager);
        return (sut, host, email);
    }

    [Fact]
    public async Task StartAsync_creates_no_user_row_and_sends_otp_email_when_new()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;

        var result = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "New User", "0900000000"));

        Assert.True(result.Success);
        Assert.NotNull(result.AttemptId);
        Assert.Empty(fixture.Fixture.Context.Users);
        Assert.Single(email.Sent);
        Assert.Equal("new@example.com", email.Sent[0].To);
    }

    [Fact]
    public async Task StartAsync_returns_email_already_used()
    {
        var (sut, fixture, _) = CreateSut();
        using var _ = fixture;
        await fixture.UserManager.CreateAsync(NewExistingUser("taken@example.com", null), "Password1");

        var result = await sut.StartAsync(new RegisterRequest("taken@example.com", "Password123!", "New User", null));

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.EmailAlreadyUsed, result.Error);
    }

    [Fact]
    public async Task StartAsync_returns_phone_already_used()
    {
        var (sut, fixture, _) = CreateSut();
        using var _ = fixture;
        fixture.Fixture.Context.Users.Add(NewExistingUser("other@example.com", "0900000000"));
        await fixture.Fixture.Context.SaveChangesAsync();

        var result = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "New User", "0900000000"));

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.PhoneAlreadyUsed, result.Error);
    }

    [Fact]
    public async Task ConfirmAsync_creates_user_with_verifiable_hashed_password_when_otp_valid()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;
        var start = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "New User", null));
        var sentCode = ExtractCode(email.Sent[0].Body);

        var confirm = await sut.ConfirmAsync(start.AttemptId!, sentCode);

        Assert.True(confirm.Success);
        Assert.NotNull(confirm.UserId);
        var user = Assert.Single(fixture.Fixture.Context.Users);
        Assert.Equal("new@example.com", user.Email);
        Assert.NotEqual("Password123!", user.PasswordHash);
        var verify = new Microsoft.AspNetCore.Identity.PasswordHasher<User>()
            .VerifyHashedPassword(user, user.PasswordHash, "Password123!");
        Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success, verify);
    }

    [Fact]
    public async Task ConfirmAsync_returns_invalid_otp_when_code_wrong()
    {
        var (sut, fixture, _) = CreateSut();
        using var _ = fixture;
        var start = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "New User", null));

        var confirm = await sut.ConfirmAsync(start.AttemptId!, "000000");

        Assert.False(confirm.Success);
        Assert.Equal(RegistrationError.InvalidOtp, confirm.Error);
        Assert.Empty(fixture.Fixture.Context.Users);
    }

    // A different code than the one sent, never the real one by chance.
    private static string Wrong(string code) => (code[0] == '9' ? "0" : ((char)(code[0] + 1)).ToString()) + code[1..];

    // Without a cap the 6-digit code could be guessed in its 5 minutes and an
    // account made under someone else's email (found in review 2026-10-08).
    // Five, the same number as the sign-in lockout agreed on 2026-10-05.
    [Fact]
    public async Task ConfirmAsync_drops_the_attempt_after_five_wrong_codes()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;
        var start = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "New User", null));
        var code = ExtractCode(email.Sent[0].Body);

        var errors = new List<RegistrationError?>();
        for (var i = 0; i < 5; i++)
            errors.Add((await sut.ConfirmAsync(start.AttemptId!, Wrong(code))).Error);
        var right = await sut.ConfirmAsync(start.AttemptId!, code);

        Assert.Equal([RegistrationError.InvalidOtp, RegistrationError.InvalidOtp, RegistrationError.InvalidOtp, RegistrationError.InvalidOtp, RegistrationError.TooManyAttempts], errors);
        Assert.Equal(RegistrationError.AttemptNotFound, right.Error);
        Assert.Empty(fixture.Fixture.Context.Users);
    }

    [Fact]
    public async Task ConfirmAsync_still_accepts_the_right_code_after_four_wrong_ones()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;
        var start = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "New User", null));
        var code = ExtractCode(email.Sent[0].Body);
        for (var i = 0; i < 4; i++)
            await sut.ConfirmAsync(start.AttemptId!, Wrong(code));

        Assert.True((await sut.ConfirmAsync(start.AttemptId!, code)).Success);
    }

    [Fact]
    public async Task ConfirmAsync_returns_attempt_not_found_when_unknown()
    {
        var (sut, fixture, _) = CreateSut();
        using var _ = fixture;

        var confirm = await sut.ConfirmAsync("unknown-attempt-id", "123456");

        Assert.False(confirm.Success);
        Assert.Equal(RegistrationError.AttemptNotFound, confirm.Error);
    }

    [Fact]
    public async Task StartAsync_returns_email_already_used_when_only_case_differs()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;
        await fixture.UserManager.CreateAsync(NewExistingUser("Taken@Example.com", null), "Password1");

        var result = await sut.StartAsync(new RegisterRequest("taken@example.com", "Password123!", "New User", null));

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.EmailAlreadyUsed, result.Error);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task StartAsync_rejects_password_shorter_than_8_without_sending_otp()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;

        var result = await sut.StartAsync(new RegisterRequest("new@example.com", "short1", "New User", null));

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.PasswordTooWeak, result.Error);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task ConfirmAsync_creates_a_user_identity_can_sign_in()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;
        var start = await sut.StartAsync(new RegisterRequest("New@Example.com", "Password123!", "New User", null));

        await sut.ConfirmAsync(start.AttemptId!, ExtractCode(email.Sent[0].Body));

        var found = await fixture.UserManager.FindByEmailAsync("new@example.com");
        Assert.NotNull(found);
        Assert.False(string.IsNullOrEmpty(found.SecurityStamp));
        Assert.True(await fixture.UserManager.CheckPasswordAsync(found, "Password123!"));
    }

    [Fact]
    public async Task ConfirmAsync_second_pending_attempt_for_same_email_returns_email_already_used()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;
        var first = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "First", null));
        var second = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "Second", null));
        await sut.ConfirmAsync(first.AttemptId!, ExtractCode(email.Sent[0].Body));

        var confirm = await sut.ConfirmAsync(second.AttemptId!, ExtractCode(email.Sent[1].Body));

        Assert.False(confirm.Success);
        Assert.Equal(RegistrationError.EmailAlreadyUsed, confirm.Error);
        Assert.Single(fixture.Fixture.Context.Users);
    }

    private static User NewExistingUser(string email, string? phone) => new()
    {
        Email = email,
        PasswordHash = "irrelevant-hash",
        FullName = "Existing User",
        Phone = phone,
        Role = UserRole.Customer,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static string ExtractCode(string body) => Regex.Match(body, @"\d{6}").Value;
}
