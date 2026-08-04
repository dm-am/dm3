using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Recovery;

/// <summary>
/// DTO for completing password reset via token
/// </summary>
/// <remarks>
/// The token is the credential of the call and travels in the X-Dm-Account-Token
/// header, not in the path.
/// Used with POST /v1/account/password-reset.
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
