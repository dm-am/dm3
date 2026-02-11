using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user registration (email-first flow).
/// Login is chosen after email verification.
/// </summary>
public class Registration
{
    /// <summary>
    /// Valid email address for account verification
    /// </summary>
    /// <example>user@example.com</example>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
    public string Email { get; set; } = "";

    /// <summary>
    /// Account password (minimum 8 characters)
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters")]
    public string Password { get; set; } = "";

    /// <summary>
    /// Indicates whether user has accepted site rules
    /// </summary>
    [Required(ErrorMessage = "You must accept the site rules")]
    public bool AcceptedRules { get; set; }

    /// <summary>
    /// Honeypot field for bot protection - must be left empty
    /// </summary>
    /// <remarks>
    /// This field is invisible to users but often filled by automated bots.
    /// If this field contains any value, the registration will be rejected.
    /// </remarks>
    public string? Website { get; set; }
}
