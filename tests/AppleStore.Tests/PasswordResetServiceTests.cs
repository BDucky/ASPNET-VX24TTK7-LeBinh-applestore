using System.Text.RegularExpressions;
using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Tests;

public class PasswordResetServiceTests
{
    private const string Email = "user@example.com";

    private static async Task<(PasswordResetService Sut, IdentityTestHost Host, FakeEmailSender Mail, User User)> CreateSut()
    {
        var host = new IdentityTestHost();
        var user = new User { Email = Email, FullName = "User", Role = UserRole.Customer, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Assert.True((await host.UserManager.CreateAsync(user, "OldPassword1")).Succeeded);
        var mail = new FakeEmailSender();
        return (new PasswordResetService(host.Fixture.Context, host.UserManager, new OtpService(), mail), host, mail, user);
    }

    private static string Code(FakeEmailSender mail) => Regex.Match(mail.Sent[^1].Body, @"\d{6}").Value;

    [Fact]
    public async Task StartAsync_says_no_account_for_an_unknown_email_and_sends_nothing()
    {
        var (sut, host, mail, _) = await CreateSut();
        using var _ = host;

        var result = await sut.StartAsync("nobody@example.com");

        Assert.Equal(PasswordResetError.NoAccount, result.Error);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task StartAsync_emails_a_code_and_stores_only_its_hash()
    {
        var (sut, host, mail, user) = await CreateSut();
        using var _ = host;

        var result = await sut.StartAsync("USER@example.com");

        Assert.True(result.Success);
        Assert.Equal(Email, Assert.Single(mail.Sent).To);
        var token = Assert.Single(host.Fixture.Context.UserTokens);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(UserTokenType.ResetPasswordOtp, token.Type);
        Assert.NotEqual(Code(mail), token.Token);
        Assert.Null(token.UsedAt);
        Assert.True(token.ExpiredAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ResetAsync_with_the_right_code_changes_the_password_once()
    {
        var (sut, host, mail, user) = await CreateSut();
        using var _ = host;
        var stampBefore = user.SecurityStamp;
        await sut.StartAsync(Email);

        var result = await sut.ResetAsync(Email, Code(mail), "NewPassword1");

        Assert.True(result.Success);
        Assert.Equal(user.Id, result.UserId);
        var saved = await host.UserManager.FindByEmailAsync(Email);
        Assert.True(await host.UserManager.CheckPasswordAsync(saved!, "NewPassword1"));
        Assert.False(await host.UserManager.CheckPasswordAsync(saved!, "OldPassword1"));
        Assert.NotEqual(stampBefore, saved!.SecurityStamp);
        Assert.NotNull((await host.Fixture.Context.UserTokens.SingleAsync()).UsedAt);

        var again = await sut.ResetAsync(Email, Code(mail), "OtherPassword1");
        Assert.Equal(PasswordResetError.CodeExpired, again.Error);
    }

    [Fact]
    public async Task ResetAsync_with_a_wrong_code_counts_a_failure_and_keeps_the_password()
    {
        var (sut, host, mail, _) = await CreateSut();
        using var _ = host;
        await sut.StartAsync(Email);
        var wrong = Code(mail) == "000000" ? "111111" : "000000";

        var result = await sut.ResetAsync(Email, wrong, "NewPassword1");

        Assert.Equal(PasswordResetError.InvalidCode, result.Error);
        var saved = await host.UserManager.FindByEmailAsync(Email);
        Assert.Equal(1, saved!.AccessFailedCount);
        Assert.True(await host.UserManager.CheckPasswordAsync(saved, "OldPassword1"));
    }

    [Fact]
    public async Task Five_wrong_codes_lock_the_account_and_then_even_the_right_code_is_refused()
    {
        var (sut, host, mail, _) = await CreateSut();
        using var _ = host;
        await sut.StartAsync(Email);
        var right = Code(mail);
        var wrong = right == "000000" ? "111111" : "000000";
        for (var i = 0; i < 4; i++)
            Assert.Equal(PasswordResetError.InvalidCode, (await sut.ResetAsync(Email, wrong, "NewPassword1")).Error);

        Assert.Equal(PasswordResetError.LockedOut, (await sut.ResetAsync(Email, wrong, "NewPassword1")).Error);
        Assert.Equal(PasswordResetError.LockedOut, (await sut.ResetAsync(Email, right, "NewPassword1")).Error);
    }

    [Fact]
    public async Task ResetAsync_refuses_an_expired_code()
    {
        var (sut, host, mail, _) = await CreateSut();
        using var _ = host;
        await sut.StartAsync(Email);
        var token = await host.Fixture.Context.UserTokens.SingleAsync();
        token.ExpiredAt = DateTime.UtcNow.AddSeconds(-1);
        await host.Fixture.Context.SaveChangesAsync();

        var result = await sut.ResetAsync(Email, Code(mail), "NewPassword1");

        Assert.Equal(PasswordResetError.CodeExpired, result.Error);
    }

    [Fact]
    public async Task A_new_code_replaces_the_previous_one()
    {
        var (sut, host, mail, _) = await CreateSut();
        using var _ = host;
        await sut.StartAsync(Email);
        var first = Code(mail);
        await sut.StartAsync(Email);
        var second = Code(mail);

        if (first != second)
            Assert.Equal(PasswordResetError.CodeExpired, (await sut.ResetAsync(Email, first, "NewPassword1")).Error);
        Assert.True((await sut.ResetAsync(Email, second, "NewPassword1")).Success);
    }

    [Fact]
    public async Task A_short_new_password_is_refused_without_spending_the_code()
    {
        var (sut, host, mail, _) = await CreateSut();
        using var _ = host;
        await sut.StartAsync(Email);

        var weak = await sut.ResetAsync(Email, Code(mail), "short1");
        var ok = await sut.ResetAsync(Email, Code(mail), "NewPassword1");

        Assert.Equal(PasswordResetError.PasswordTooWeak, weak.Error);
        Assert.True(ok.Success);
    }

    [Fact]
    public async Task ResetAsync_for_an_unknown_email_is_no_account()
    {
        var (sut, host, _, _) = await CreateSut();
        using var _ = host;

        var result = await sut.ResetAsync("nobody@example.com", "123456", "NewPassword1");

        Assert.Equal(PasswordResetError.NoAccount, result.Error);
    }
}
