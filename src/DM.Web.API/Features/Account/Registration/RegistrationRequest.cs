using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// Request for user registration (email-first flow).
/// Username is chosen after email verification.
/// </summary>
public class RegistrationRequest
{
    /// <summary>
    /// Valid email address for account verification
    /// </summary>
    /// <example>user@example.com</example>
    [Required(ErrorMessage = "Почта обязательна")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    [StringLength(100, ErrorMessage = "Почта не должна превышать 100 символов")]
    public string Email { get; set; } = "";

    /// <summary>
    /// Account password (minimum 8 characters)
    /// </summary>
    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен быть от 8 до 128 символов")]
    public string Password { get; set; } = "";

    /// <summary>
    /// Indicates whether user has accepted site rules
    /// </summary>
    [Required(ErrorMessage = "Необходимо принять правила сайта")]
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
