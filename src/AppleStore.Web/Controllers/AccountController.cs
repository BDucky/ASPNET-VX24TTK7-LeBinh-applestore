using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// Use cases 1-3 (register with OTP, send OTP, log in) plus log out, built on
// ASP.NET Core Identity: UserManager finds users, SignInManager checks
// passwords, counts failures toward lockout, and writes the sign-in cookie.
public class AccountController : Controller
{
    private const string LoginFailed = "Email or password is incorrect.";

    private readonly IRegistrationService _registration;
    private readonly UserManager<User> _users;
    private readonly SignInManager<User> _signIn;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IRegistrationService registration,
        UserManager<User> users,
        SignInManager<User> signIn,
        ILogger<AccountController> logger)
    {
        _registration = registration;
        _users = users;
        _signIn = signIn;
        _logger = logger;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
        {
            // Cookie points at a user that no longer exists.
            await _signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        return View(new AccountSummaryViewModel(user.FullName, user.Email, user.Phone, user.Role.ToString(), user.CreatedAt));
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        var result = await _registration.StartAsync(
            new RegisterRequest(model.Email.Trim(), model.Password, model.FullName.Trim(), phone), ct);

        if (!result.Success)
        {
            var (field, message) = result.Error switch
            {
                RegistrationError.EmailAlreadyUsed => (nameof(model.Email), "An account with this email already exists."),
                RegistrationError.PhoneAlreadyUsed => (nameof(model.Phone), "This phone number is already used by another account."),
                RegistrationError.PasswordTooWeak => (nameof(model.Password), "Password must be at least 8 characters."),
                _ => (string.Empty, "Registration failed. Please try again."),
            };
            ModelState.AddModelError(field, message);
            return View(model);
        }

        return RedirectToAction(nameof(VerifyOtp), new { attemptId = result.AttemptId, email = model.Email.Trim() });
    }

    [HttpGet]
    public IActionResult VerifyOtp(string? attemptId, string? email)
    {
        // Without an attempt there is no code to check; start over instead of
        // showing a form that can only fail.
        if (string.IsNullOrWhiteSpace(attemptId))
            return RedirectToAction(nameof(Register));

        return View(new VerifyOtpViewModel { AttemptId = attemptId, Email = email ?? string.Empty });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.AttemptId))
            return RedirectToAction(nameof(Register));
        if (!ModelState.IsValid)
            return View(model);

        var result = await _registration.ConfirmAsync(model.AttemptId, model.Code.Trim(), ct);
        if (!result.Success)
        {
            ViewData["OtpExpired"] = result.Error is RegistrationError.AttemptNotFound or RegistrationError.EmailAlreadyUsed;
            ModelState.AddModelError(string.Empty, result.Error switch
            {
                RegistrationError.InvalidOtp => "The code is incorrect or has expired.",
                RegistrationError.AttemptNotFound => "This registration has expired. Please register again.",
                RegistrationError.EmailAlreadyUsed => "An account with this email already exists. Please sign in.",
                _ => "Verification failed. Please try again.",
            });
            return View(model);
        }

        var user = await _users.FindByIdAsync(result.UserId!.Value.ToString());
        if (user is null)
        {
            _logger.LogError("User {UserId} was created by OTP confirmation but cannot be found", result.UserId);
            return RedirectToAction(nameof(Login));
        }

        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        model.ReturnUrl ??= returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        // lockoutOnFailure: every wrong password counts toward Identity's lockout.
        var result = await _signIn.PasswordSignInAsync(model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Too many failed attempts. This account is locked for a few minutes."
            : LoginFailed);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return LocalRedirect("/");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
