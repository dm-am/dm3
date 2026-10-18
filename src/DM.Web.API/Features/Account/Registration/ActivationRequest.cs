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
    /// IANA name of the time zone of the device the account is created on
    /// ("Europe/Moscow"), optional
    /// </summary>
    /// <remarks>
    /// Written into the new account's settings, where the letters read it: the
    /// site formats a date in the browser, a letter has no browser. This request is
    /// the one that creates the account, so it is the one that carries the zone.
    /// A name that is no IANA zone this server knows, or an empty one, is left out
    /// and the account keeps UTC; the account is created either way. Bounded like
    /// the zone of the preferences.
    /// </remarks>
    /// <example>Europe/Moscow</example>
    [StringLength(64, ErrorMessage = "Название часового пояса не должно превышать 64 символа")]
    public string? TimeZoneId { get; set; }
}
