using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Deactivation;

/// <summary>
/// Request for account deactivation
/// </summary>
/// <remarks>
/// Password required to confirm the action.
/// </remarks>
public class DeactivationRequest
{
    /// <summary>
    /// Current password for confirmation
    /// </summary>
    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Пароль обязателен")]
    public string Password { get; set; } = "";
}
