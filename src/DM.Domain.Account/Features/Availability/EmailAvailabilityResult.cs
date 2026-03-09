namespace DM.Domain.Account.Features.Availability;

/// <summary>
/// Result of email availability check
/// </summary>
public class EmailAvailabilityResult
{
    /// <summary>
    /// Whether the email is available for registration
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Reason why email is not available (if applicable)
    /// </summary>
    public EmailUnavailableReason? Reason { get; init; }

    /// <summary>
    /// Create available result
    /// </summary>
    public static EmailAvailabilityResult Available() => new() { IsAvailable = true };

    /// <summary>
    /// Create unavailable result
    /// </summary>
    public static EmailAvailabilityResult Unavailable(EmailUnavailableReason reason) =>
        new() { IsAvailable = false, Reason = reason };
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
