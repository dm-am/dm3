namespace DM.Services.Community.BusinessProcesses.Account.Recovery;

/// <summary>
/// Result of email availability check
/// </summary>
public class EmailAvailabilityResult
{
    /// <summary>
    /// Whether the email is available for registration
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Reason why email is not available (if applicable)
    /// </summary>
    public EmailUnavailableReason? Reason { get; set; }

    /// <summary>
    /// Create available result
    /// </summary>
    public static EmailAvailabilityResult IsAvailable() => new() { Available = true };

    /// <summary>
    /// Create unavailable result
    /// </summary>
    public static EmailAvailabilityResult Unavailable(EmailUnavailableReason reason) =>
        new() { Available = false, Reason = reason };
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
