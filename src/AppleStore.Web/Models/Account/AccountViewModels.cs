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

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code.")]
    [Display(Name = "Code from the email")]
    public string Code { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }
}

public class AddressViewModel
{
    [StringLength(60), Display(Name = "Label (for example Home, Office)")]
    public string? Label { get; set; }

    [Required, StringLength(120), Display(Name = "Receiver name")]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(20), Display(Name = "Receiver phone")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(255), Display(Name = "Street address")]
    public string AddressLine { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Ward { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [StringLength(100), Display(Name = "City or province")]
    public string? City { get; set; }

    [Display(Name = "Use as my default address")]
    public bool IsDefault { get; set; }
}
