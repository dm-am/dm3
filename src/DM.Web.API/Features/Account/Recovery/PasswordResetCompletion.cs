using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Recovery;

/// <summary>
/// DTO for completing password reset via token
/// </summary>
/// <remarks>
/// Token is passed in URL path.
/// Used with POST /v1/account/password-reset/{token}.
/// </remarks>
public class PasswordResetCompletion
{
    /// <summary>
    /// New password (minimum 8 characters)
    /// </summary>
    [Required(ErrorMessage = "Новый пароль обязателен")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен быть от 8 до 128 символов")]
    public string NewPassword { get; set; } = "";
}
