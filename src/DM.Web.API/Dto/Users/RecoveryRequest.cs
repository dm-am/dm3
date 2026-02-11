using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Request for account recovery (password reset or activation resend)
/// </summary>
public class RecoveryRequest
{
    /// <summary>
    /// Email address to recover
    /// </summary>
    /// <example>user@example.com</example>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = "";
}
