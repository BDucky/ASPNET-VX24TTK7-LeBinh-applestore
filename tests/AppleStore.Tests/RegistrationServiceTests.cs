using System.Text.RegularExpressions;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;

namespace AppleStore.Tests;

public class RegistrationServiceTests
{
    private static (RegistrationService Sut, SqliteInMemoryFixture Fixture, FakeEmailSender Email) CreateSut()
    {
        var fixture = new SqliteInMemoryFixture();
        fixture.Context.Database.EnsureCreated();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var email = new FakeEmailSender();
        var sut = new RegistrationService(fixture.Context, cache, new OtpService(), email);
        return (sut, fixture, email);
    }

    [Fact]
    public async Task StartAsync_creates_no_user_row_and_sends_otp_email_when_new()
    {
        var (sut, fixture, email) = CreateSut();
        using var _ = fixture;

        var result = await sut.StartAsync(new RegisterRequest("new@example.com", "Password123!", "New User", "0900000000"));

        Assert.True(result.Success);
        Assert.NotNull(result.AttemptId);
        Assert.Empty(fixture.Context.Users);
        Assert.Single(email.Sent);
        Assert.Equal("new@example.com", email.Sent[0].To);
    }

    [Fact]
    public async Task StartAsync_returns_email_already_used()
    {
        var (sut, fixture, _) = CreateSut();
        using var _ = fixture;
        fixture.Context.Users.Add(NewExistingUser("taken@example.com", null));
        await fixture.Context.SaveChangesAsync();

        var result = await sut.StartAsync(new RegisterRequest("taken@example.com", "Password123!", "New User", null));

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.EmailAlreadyUsed, result.Error);
    }

    [Fact]
    public async Task StartAsync_returns_phone_already_used()
    {
        var (sut, fixture, _) = CreateSut();
        using var _ = fixture;
        fixture.Context.Users.Add(NewExistingUser("other@example.com", "0900000000"));
        await fixture.Context.SaveChangesAsync();

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
        var user = Assert.Single(fixture.Context.Users);
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
        Assert.Empty(fixture.Context.Users);
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
