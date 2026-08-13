using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// DTO model for resending activation email
/// </summary>
/// <remarks>
/// Used to resend the activation email if the user didn't receive it
/// or the original email expired. For security, the API returns success
/// even if no matching inactive account is found.
/// </remarks>
public class ResendActivation
{
    /// <summary>
    /// User email address (must match the account's registered email)
    /// </summary>
    /// <example>user@example.com</example>
    [Required(ErrorMessage = "Введите почту")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    [StringLength(100, ErrorMessage = "Почта не длиннее 100 символов")]
    public string Email { get; set; } = "";
}
