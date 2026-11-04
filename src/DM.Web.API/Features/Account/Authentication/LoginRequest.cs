using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.BotTrap;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Request for user authentication
/// </summary>
public class LoginRequest : IHoneypotForm
{
    /// <summary>
    /// User email address
    /// </summary>
    /// <example>john@example.com</example>
    [Required(ErrorMessage = "Почта обязательна")]
    [StringLength(100, ErrorMessage = "Почта должна быть от 1 до 100 символов")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    public string Email { get; set; } = "";

    /// <summary>
    /// User password
    /// </summary>
    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(128, ErrorMessage = "Пароль должен быть от 1 до 128 символов")]
    public string Password { get; set; } = "";

    /// <summary>
    /// Honeypot field for bot protection - must be left empty
    /// </summary>
    public string? Website { get; set; }

    /// <inheritdoc />
    (string Field, string Sentence) IHoneypotForm.Refusal => ("email", RefusalMessage.LoginFailed);

    /// <summary>
    /// Keep the session past the browser being closed
    /// </summary>
    /// <remarks>
    /// The longer of the two configured lifetimes, counted from the login and
    /// extended near its end, not a window of inactivity.
    /// </remarks>
    public bool RememberMe { get; set; } = true;
}
