using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// Request for completing activation with username selection
/// </summary>
public class ActivationRequest
{
    /// <summary>
    /// Chosen username (unique display name, 2-20 characters)
    /// </summary>
    /// <remarks>
    /// The characters are the domain validator's rule, so the availability check and
    /// the form answer alike; length and presence stay for the published schema.
    /// </remarks>
    /// <example>JohnDoe_123</example>
    [Required(ErrorMessage = "Имя пользователя обязательно")]
    // Two at least: a name is the one address of a profile, and one-letter names would go to the first comers.
    [StringLength(20, MinimumLength = 2, ErrorMessage = "От 2 до 20 символов")]
    public string Username { get; set; } = "";

    /// <summary>
    /// IANA name of the time zone of the device the account is created on
    /// ("Europe/Moscow"), optional
    /// </summary>
    /// <remarks>
    /// For the letters, which have no browser to format a date. An unknown or empty
    /// zone leaves the account on UTC.
    /// </remarks>
    /// <example>Europe/Moscow</example>
    [StringLength(64, ErrorMessage = "Название часового пояса не должно превышать 64 символа")]
    public string? TimeZoneId { get; set; }
}
