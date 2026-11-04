using DM.Domain.Account.Features.Availability;

namespace DM.Web.API.Features.Account.Availability;

/// <summary>
/// Response for email availability check
/// </summary>
public class EmailAvailabilityResponse
{
    /// <summary>
    /// Whether the email is available for registration
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Reason if email is not available
    /// </summary>
    public EmailUnavailableReason? Reason { get; set; }
}
