using System.Net;
using System.Text.RegularExpressions;

namespace AppleStore.Tests;

public class AccountFlowTests : WebFlowTestBase
{
    [Fact]
    public async Task Account_page_sends_anonymous_visitors_to_login()
    {
        var response = await Client.GetAsync("/Account");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Register_then_correct_otp_signs_the_new_user_in()
    {
        var register = await PostFormAsync("/Account/Register", new()
        {
            ["Email"] = "new@example.com",
            ["FullName"] = "Nguyen Van A",
            ["Phone"] = "",
            ["Password"] = "Password1",
            ["ConfirmPassword"] = "Password1",
        });
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);
        var verifyUrl = register.Headers.Location!.OriginalString;
        Assert.StartsWith("/Account/VerifyOtp", verifyUrl);

        var code = Regex.Match(Assert.Single(Factory.Email.Sent).Body, @"\d{6}").Value;
        var verify = await PostFormAsync(verifyUrl, new() { ["Code"] = code });
        Assert.Equal(HttpStatusCode.Redirect, verify.StatusCode);

        var account = await Client.GetStringAsync("/Account");
        Assert.Contains("Nguyen Van A", account);
        Assert.Contains("new@example.com", account);
    }

    [Fact]
    public async Task Wrong_otp_keeps_the_visitor_on_the_page_and_creates_no_account()
    {
        var register = await PostFormAsync("/Account/Register", new()
        {
            ["Email"] = "new@example.com",
            ["FullName"] = "Nguyen Van A",
            ["Password"] = "Password1",
            ["ConfirmPassword"] = "Password1",
        });
        var verifyUrl = register.Headers.Location!.OriginalString;

        var verify = await PostFormAsync(verifyUrl, new() { ["Code"] = "000000" });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Contains("code is incorrect", await verify.Content.ReadAsStringAsync());
        Assert.Null(await FindUserAsync("new@example.com"));
    }

    [Fact]
    public async Task Register_with_a_short_password_shows_the_rule_and_sends_no_email()
    {
        var register = await PostFormAsync("/Account/Register", new()
        {
            ["Email"] = "new@example.com",
            ["FullName"] = "Nguyen Van A",
            ["Password"] = "short1",
            ["ConfirmPassword"] = "short1",
        });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.Contains("at least 8 characters", await register.Content.ReadAsStringAsync());
        Assert.Empty(Factory.Email.Sent);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_show_the_same_message()
    {
        await CreateUserAsync("user@example.com", "Password1");

        var wrongPassword = await LoginAsync("user@example.com", "WrongPass1");
        var unknownEmail = await LoginAsync("nobody@example.com", "Password1");

        Assert.Equal(HttpStatusCode.OK, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknownEmail.StatusCode);
        var a = ErrorMessage(await wrongPassword.Content.ReadAsStringAsync());
        var b = ErrorMessage(await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal("Email or password is incorrect.", a);
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Login_follows_a_local_return_url()
    {
        await CreateUserAsync("user@example.com", "Password1");

        var response = await LoginAsync("user@example.com", "Password1", "/Account");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Login_ignores_an_external_return_url()
    {
        await CreateUserAsync("user@example.com", "Password1");

        var response = await LoginAsync("user@example.com", "Password1", "https://evil.example/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_out_even_the_right_password()
    {
        await CreateUserAsync("user@example.com", "Password1");
        for (var i = 0; i < 5; i++)
            await LoginAsync("user@example.com", "WrongPass1");

        var response = await LoginAsync("user@example.com", "Password1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("locked", ErrorMessage(await response.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await Client.GetAsync("/Account")).StatusCode);
    }

    [Fact]
    public async Task Logout_needs_a_post_and_then_signs_the_user_out()
    {
        await CreateUserAsync("user@example.com", "Password1");
        await LoginAsync("user@example.com", "Password1");

        await Client.GetAsync("/Account/Logout");
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/Account")).StatusCode);

        var logout = await PostFormAsync("/Account/Logout", new(), formPage: "/Account");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Client.GetAsync("/Account")).StatusCode);
    }

    [Fact]
    public async Task Nav_shows_sign_in_when_anonymous_and_the_name_when_signed_in()
    {
        Assert.Contains("Sign in", await Client.GetStringAsync("/"));

        await CreateUserAsync("user@example.com", "Password1", "Tran Thi B");
        await LoginAsync("user@example.com", "Password1");

        var home = await Client.GetStringAsync("/");
        Assert.Contains("Tran Thi B", home);
        Assert.DoesNotContain(">Sign in<", home);
    }

    [Fact]
    public async Task Verify_page_without_a_registration_sends_the_visitor_to_register()
    {
        var response = await Client.GetAsync("/Account/VerifyOtp");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Register", response.Headers.Location!.OriginalString);
    }
}
