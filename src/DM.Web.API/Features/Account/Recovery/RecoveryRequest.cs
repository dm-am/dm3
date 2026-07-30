using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Recovery;

/// <summary>
/// Request for account recovery (password reset or activation resend)
/// </summary>
public class RecoveryRequest
{
    /// <summary>
    /// Email address to recover
    /// </summary>
    /// <example>user@example.com</example>
    [Required(ErrorMessage = "Почта обязательна")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    public string Email { get; set; } = "";
}
