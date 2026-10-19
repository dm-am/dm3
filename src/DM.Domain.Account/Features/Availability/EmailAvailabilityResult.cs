namespace DM.Domain.Account.Features.Availability;

public class EmailAvailabilityResult
{
    /// <summary>
    /// Whether the email is available for registration
    /// </summary>
    public bool IsAvailable => Reason == null;

    /// <summary>
    /// Reason why email is not available (if applicable)
    /// </summary>
    public EmailUnavailableReason? Reason { get; init; }

    /// <summary>
    /// Create available result
    /// </summary>
    public static EmailAvailabilityResult Available() => new();

    /// <summary>
    /// Create unavailable result
    /// </summary>
    public static EmailAvailabilityResult Unavailable(EmailUnavailableReason reason) =>
        new() { Reason = reason };
}

/// <summary>
/// Reason why email is not available
/// </summary>
public enum EmailUnavailableReason
{
    /// <summary>
    /// Email is held by an account, deactivated accounts included
    /// </summary>
    Taken,

    /// <summary>
    /// Email has a pending registration
    /// </summary>
    PendingActivation
}
