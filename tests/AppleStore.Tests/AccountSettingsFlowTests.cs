using System.Net;
using System.Text.RegularExpressions;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppleStore.Tests;

// Use cases 4-6 through the real app: forgot password, change password,
// profile and delivery addresses.
public class AccountSettingsFlowTests : WebFlowTestBase
{
    private const string Email = "user@example.com";

    [Fact]
    public async Task Login_page_links_to_forgot_password()
    {
        Assert.Contains("href=\"/Account/ForgotPassword\"", await Client.GetStringAsync("/Account/Login"));
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_says_so_and_offers_register()
    {
        var response = await PostFormAsync("/Account/ForgotPassword", new() { ["Email"] = "nobody@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("No account uses this email.", html);
        Assert.Contains("href=\"/Account/Register\"", html);
        Assert.Empty(Factory.Email.Sent);
    }

    [Fact]
    public async Task Forgot_password_then_right_code_sets_the_new_password_and_signs_in()
    {
        await CreateUserAsync(Email, "OldPassword1", "Reset User");

        var start = await PostFormAsync("/Account/ForgotPassword", new() { ["Email"] = Email });
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var resetUrl = start.Headers.Location!.OriginalString;
        Assert.StartsWith("/Account/ResetPassword", resetUrl);

        var code = Regex.Match(Assert.Single(Factory.Email.Sent).Body, @"\d{6}").Value;
        var reset = await PostFormAsync(resetUrl, new()
        {
            ["Code"] = code,
            ["NewPassword"] = "NewPassword1",
            ["ConfirmPassword"] = "NewPassword1",
        });
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Contains("Reset User", await Client.GetStringAsync("/Account"));

        using var fresh = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(fresh, Email, "OldPassword1")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(fresh, Email, "NewPassword1")).StatusCode);
    }

    [Fact]
    public async Task Reset_with_a_wrong_code_stays_on_the_page()
    {
        await CreateUserAsync(Email, "OldPassword1");
        var start = await PostFormAsync("/Account/ForgotPassword", new() { ["Email"] = Email });
        var code = Regex.Match(Factory.Email.Sent[0].Body, @"\d{6}").Value;

        var reset = await PostFormAsync(start.Headers.Location!.OriginalString, new()
        {
            ["Code"] = code == "000000" ? "111111" : "000000",
            ["NewPassword"] = "NewPassword1",
            ["ConfirmPassword"] = "NewPassword1",
        });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal("The code is incorrect or has expired.", ErrorMessage(await reset.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Change_password_needs_sign_in()
    {
        var response = await Client.GetAsync("/Account/ChangePassword");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Change_password_refuses_a_wrong_current_password()
    {
        await CreateUserAsync(Email, "OldPassword1");
        await LoginAsync(Email, "OldPassword1");

        var response = await PostFormAsync("/Account/ChangePassword", new()
        {
            ["CurrentPassword"] = "WrongPassword1",
            ["NewPassword"] = "NewPassword1",
            ["ConfirmPassword"] = "NewPassword1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Current password is incorrect.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Change_password_keeps_this_session_and_the_new_password_works()
    {
        await CreateUserAsync(Email, "OldPassword1");
        await LoginAsync(Email, "OldPassword1");

        var response = await PostFormAsync("/Account/ChangePassword", new()
        {
            ["CurrentPassword"] = "OldPassword1",
            ["NewPassword"] = "NewPassword1",
            ["ConfirmPassword"] = "NewPassword1",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var account = await Client.GetAsync("/Account");
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.Contains("Your password was changed.", await account.Content.ReadAsStringAsync());
        using var fresh = NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(fresh, Email, "NewPassword1")).StatusCode);
    }

    [Fact]
    public async Task Profile_update_shows_the_new_name_in_the_nav()
    {
        await CreateUserAsync(Email, "Password1", "Old Name");
        await LoginAsync(Email, "Password1");

        var response = await PostFormAsync("/Account/Profile", new() { ["FullName"] = "New Name", ["Phone"] = "0912345678" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var home = await Client.GetStringAsync("/");
        Assert.Contains("New Name", home);
        Assert.Contains("0912345678", await Client.GetStringAsync("/Account"));
    }

    [Fact]
    public async Task Profile_refuses_a_phone_another_account_uses()
    {
        await CreateUserAsync("other@example.com", "Password1", phone: "0900000002");
        await CreateUserAsync(Email, "Password1");
        await LoginAsync(Email, "Password1");

        var response = await PostFormAsync("/Account/Profile", new() { ["FullName"] = "Name", ["Phone"] = "0900000002" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This phone number is already used by another account.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Added_address_is_listed_as_default()
    {
        await CreateUserAsync(Email, "Password1");
        await LoginAsync(Email, "Password1");

        var add = await PostFormAsync("/Account/Addresses/Create", AddressForm("12 Nguyen Hue"));

        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        var list = await Client.GetStringAsync("/Account/Addresses");
        Assert.Contains("12 Nguyen Hue", list);
        Assert.Contains("Default", list);
    }

    [Fact]
    public async Task Another_users_address_is_not_found()
    {
        await CreateUserAsync("other@example.com", "Password1");
        using var other = NewClient();
        await LoginAsync(other, "other@example.com", "Password1");
        await PostFormAsync(other, "/Account/Addresses/Create", AddressForm("Theirs"));
        var theirId = await FirstAddressIdAsync();

        await CreateUserAsync(Email, "Password1");
        await LoginAsync(Email, "Password1");

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/Account/Addresses/Edit/{theirId}")).StatusCode);
        var delete = await PostFormAsync($"/Account/Addresses/Delete/{theirId}", new(), formPage: "/Account/Addresses");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(theirId, await FirstAddressIdAsync());
    }

    [Fact]
    public async Task Own_address_can_be_edited_and_deleted()
    {
        await CreateUserAsync(Email, "Password1");
        await LoginAsync(Email, "Password1");
        await PostFormAsync("/Account/Addresses/Create", AddressForm("Old line"));
        var id = await FirstAddressIdAsync();

        var edit = await PostFormAsync($"/Account/Addresses/Edit/{id}", AddressForm("New line"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Contains("New line", await Client.GetStringAsync("/Account/Addresses"));

        var delete = await PostFormAsync($"/Account/Addresses/Delete/{id}", new(), formPage: "/Account/Addresses");
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.DoesNotContain("New line", await Client.GetStringAsync("/Account/Addresses"));
    }

    private static Dictionary<string, string> AddressForm(string line) => new()
    {
        ["Label"] = "Home",
        ["FullName"] = "Nguyen Van A",
        ["Phone"] = "0911111111",
        ["AddressLine"] = line,
        ["Ward"] = "Ben Nghe",
        ["District"] = "District 1",
        ["City"] = "Ho Chi Minh City",
    };

    private async Task<int> FirstAddressIdAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Addresses.Select(a => a.Id).FirstAsync();
    }
}
