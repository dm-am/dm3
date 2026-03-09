namespace DM.Web.API.Features.Account.Availability;

/// <summary>
/// Response for username availability check
/// </summary>
public class UsernameAvailabilityResponse
{
    /// <summary>
    /// Whether the username is available for use
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Reason if not available
    /// </summary>
    public UsernameUnavailableReason? Reason { get; set; }
}

/// <summary>
/// Reason why username is not available
/// </summary>
public enum UsernameUnavailableReason
{
    /// <summary>
    /// Username is taken by another user
    /// </summary>
    Taken,

    /// <summary>
    /// Username was previously used by another user and is reserved in history
    /// </summary>
    Reserved,

    /// <summary>
    /// Username format is invalid
    /// </summary>
    InvalidFormat
}
