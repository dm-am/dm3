using System.ComponentModel.DataAnnotations;

namespace DM.Web.Core.Authentication.Credentials;

/// <summary>
/// Login credentials for authentication
/// </summary>
public class LoginCredentials : AuthCredentials
{
    /// <summary>
    /// User email address
    /// </summary>
    /// <example>john@example.com</example>
    [Required(ErrorMessage = "Email is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Email must be between 1 and 100 characters")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = "";

    /// <summary>
    /// User password
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Password must be between 1 and 128 characters")]
    public string Password { get; set; } = "";

    /// <summary>
    /// Honeypot field for bot protection - must be left empty
    /// </summary>
    /// <remarks>
    /// This field is invisible to users but often filled by automated bots.
    /// If this field contains any value, the login attempt will be rejected.
    /// </remarks>
    public string? Website { get; set; }

    /// <summary>
    /// Remember session for 1 year (true) or 24 hours (false)
    /// </summary>
    /// <remarks>
    /// When true, the session cookie will persist for 1 year.
    /// When false, the session expires after 24 hours of inactivity.
    /// Defaults to true for convenience.
    /// </remarks>
    public bool RememberMe { get; set; } = true;
}
