using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// Request for completing activation with username selection
/// </summary>
/// <remarks>
/// Token is passed in URL path, this DTO contains only the request body.
/// </remarks>
public class ActivationRequest
{
    /// <summary>
    /// Chosen username (unique display name, 2-20 characters)
    /// </summary>
    /// <remarks>
    /// Allowed: letters (Latin/Cyrillic), digits, underscore, hyphen, dot, space.
    /// Spaces: not at start/end, not consecutive.
    /// </remarks>
    /// <example>JohnDoe_123</example>
    [Required(ErrorMessage = "Имя пользователя обязательно")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "От 2 до 20 символов")]
    [RegularExpression(@"^(?!.*  )[a-zA-Zа-яА-ЯеЕ0-9]([a-zA-Zа-яА-ЯеЕ0-9_.\- ]*[a-zA-Zа-яА-ЯеЕ0-9])?$",
        ErrorMessage = "Недопустимые символы или формат")]
    public string Username { get; set; } = "";

    /// <summary>
    /// Optional: email from sessionStorage for idempotent retry detection.
    /// If activation already completed for this email+username, returns success instead of error.
    /// </summary>
    public string? RetryEmail { get; set; }
}
