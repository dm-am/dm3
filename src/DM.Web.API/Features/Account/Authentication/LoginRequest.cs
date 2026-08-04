using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Request for user authentication
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// User email address
    /// </summary>
    /// <example>john@example.com</example>
    [Required(ErrorMessage = "Почта обязательна")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Почта должна быть от 1 до 100 символов")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    public string Email { get; set; } = "";

    /// <summary>
    /// User password
    /// </summary>
    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Пароль должен быть от 1 до 128 символов")]
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
    /// Remember session for 365 days (true) or 24 hours (false)
    /// </summary>
    /// <remarks>
    /// When true, the session cookie will persist for 365 days.
    /// When false, the session expires after 24 hours of inactivity.
    /// Defaults to true for convenience.
    /// </remarks>
    public bool RememberMe { get; set; } = true;
}
