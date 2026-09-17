using AppleStore.Infrastructure.Services;

namespace AppleStore.Tests;

public class OtpServiceTests
{
    private readonly IOtpService _sut = new OtpService();

    [Fact]
    public void GenerateCode_returns_six_digit_numeric_string()
    {
        var code = _sut.GenerateCode();

        Assert.Equal(6, code.Length);
        Assert.True(code.All(char.IsDigit));
    }

    [Fact]
    public void IsValid_true_when_code_matches_and_not_expired()
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(5);

        Assert.True(_sut.IsValid("123456", "123456", expiresAt, now));
    }

    [Fact]
    public void IsValid_false_when_code_does_not_match()
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(5);

        Assert.False(_sut.IsValid("111111", "123456", expiresAt, now));
    }

    [Fact]
    public void IsValid_false_when_expired()
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(-1);

        Assert.False(_sut.IsValid("123456", "123456", expiresAt, now));
    }
}
