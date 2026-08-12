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
    /// Which characters are allowed is decided by the domain validator, not here.
    /// This carried an allow-list while the validator behind it carries a
    /// deny-list, and the two disagreed: check-availability called a name free
    /// and the form then refused the same name with 400. Length and presence
    /// stay, so the published schema keeps its bounds.
    /// </remarks>
    /// <example>JohnDoe_123</example>
    [Required(ErrorMessage = "Имя пользователя обязательно")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "От 2 до 20 символов")]
    public string Username { get; set; } = "";

    /// <summary>
    /// Optional: email from sessionStorage for idempotent retry detection.
    /// If activation already completed for this email+username, returns success instead of error.
    /// </summary>
    public string? RetryEmail { get; set; }
}
