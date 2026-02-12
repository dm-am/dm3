using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for changing user email
/// </summary>
/// <remarks>
/// Requires authentication. The current user's login is taken from the session.
/// After successful change, a confirmation email will be sent to the new address.
/// </remarks>
public class ChangeEmail
{
    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Password is required")]
    public string Password { get; set; } = "";

    /// <summary>
    /// New email address
    /// </summary>
    /// <example>newemail@example.com</example>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
    public string Email { get; set; } = "";
}
