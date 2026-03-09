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

/// <summary>
/// Reason why email is not available
/// </summary>
public enum EmailUnavailableReason
{
    /// <summary>
    /// Email is used by an active user
    /// </summary>
    Taken,

    /// <summary>
    /// Email has a pending registration
    /// </summary>
    PendingActivation
}
