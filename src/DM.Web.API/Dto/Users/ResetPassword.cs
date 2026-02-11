using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for requesting password reset
/// </summary>
/// <remarks>
/// Initiates password reset flow. An email with a reset token will be sent
/// to the user if the login and email match an existing account.
///
/// For security, the API returns success even if no matching account is found.
/// </remarks>
public class ResetPassword
{
    /// <summary>
    /// User login
    /// </summary>
    /// <example>JohnDoe</example>
    [Required(ErrorMessage = "Login is required")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Login must be between 2 and 20 characters")]
    public string Login { get; set; } = "";

    /// <summary>
    /// User email address
    /// </summary>
    /// <example>user@example.com</example>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
    public string Email { get; set; } = "";
}
