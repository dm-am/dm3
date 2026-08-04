using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Request to change user email address
/// </summary>
public class EmailChangeRequest
{
    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Пароль обязателен")]
    public string Password { get; set; } = "";

    /// <summary>
    /// New email address
    /// </summary>
    [Required(ErrorMessage = "Почта обязательна")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    [StringLength(100, ErrorMessage = "Почта не должна превышать 100 символов")]
    public string Email { get; set; } = "";
}
