using System.ComponentModel.DataAnnotations;

namespace AppleStore.Web.Models.Account;

// Password length is not repeated here as an attribute: Identity owns that
// rule (AddAppleStoreIdentityCore) and RegistrationService reports it.
public class RegisterViewModel
{
    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyOtpViewModel
{
    [Required]
    public string AttemptId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code.")]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public record AccountSummaryViewModel(string FullName, string Email, string? Phone, string Role, DateTime MemberSince);
