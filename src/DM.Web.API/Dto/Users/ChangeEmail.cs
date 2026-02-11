using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for changing user email
/// </summary>
/// <remarks>
/// After successful change, a confirmation email will be sent to the new address.
/// The old email will receive a notification about the change.
/// </remarks>
public class ChangeEmail
{
    /// <summary>
    /// User login
    /// </summary>
    /// <example>JohnDoe</example>
    [Required(ErrorMessage = "Login is required")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Login must be between 2 and 20 characters")]
    public string Login { get; set; } = "";

    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters")]
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
