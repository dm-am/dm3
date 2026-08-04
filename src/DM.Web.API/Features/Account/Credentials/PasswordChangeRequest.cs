using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Request to change password (authenticated user)
/// </summary>
/// <remarks>
/// Used when user is logged in and wants to change their password.
/// Requires current password for verification.
/// For password reset via email token, see POST /recovery and POST /password-reset.
/// </remarks>
public class PasswordChangeRequest
{
    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Текущий пароль обязателен")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Текущий пароль обязателен")]
    public string OldPassword { get; set; } = "";

    /// <summary>
    /// New password (minimum 8 characters)
    /// </summary>
    [Required(ErrorMessage = "Новый пароль обязателен")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен быть от 8 до 128 символов")]
    public string NewPassword { get; set; } = "";
}
