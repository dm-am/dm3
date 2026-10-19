using DM.Domain.Account.Features.Availability;

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
