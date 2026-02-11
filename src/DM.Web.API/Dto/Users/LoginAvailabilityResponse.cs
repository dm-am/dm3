namespace DM.Web.API.Dto.Users;

/// <summary>
/// Response for login availability check
/// </summary>
public class LoginAvailabilityResponse
{
    /// <summary>
    /// Whether the login is available for use
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Reason if not available: "taken", "reserved", "invalid_format"
    /// </summary>
    /// <example>taken</example>
    public string? Reason { get; set; }
}
