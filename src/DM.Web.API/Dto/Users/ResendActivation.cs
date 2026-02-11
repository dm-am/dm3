using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for resending activation email
/// </summary>
/// <remarks>
/// Used to resend the activation email if the user didn't receive it
/// or the original email expired. For security, the API returns success
/// even if no matching inactive account is found.
/// </remarks>
public class ResendActivation
{
    /// <summary>
    /// User email address (must match the account's registered email)
    /// </summary>
    /// <example>user@example.com</example>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
    public string Email { get; set; } = "";
}
