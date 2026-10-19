using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Request to change password (authenticated user)
/// </summary>
public class PasswordChangeRequest
{
    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Текущий пароль обязателен")]
    [StringLength(128, ErrorMessage = "Текущий пароль обязателен")]
    public string OldPassword { get; set; } = "";

    /// <summary>
    /// New password (minimum 8 characters)
    /// </summary>
    [Required(ErrorMessage = "Новый пароль обязателен")]
    // PasswordPolicyConfiguration's bounds: a shorter password falls to guessing, a longer one loads the hash.
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен быть от 8 до 128 символов")]
    public string NewPassword { get; set; } = "";
}
