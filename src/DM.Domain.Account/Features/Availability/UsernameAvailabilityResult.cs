namespace DM.Domain.Account.Features.Availability;

/// <summary>
/// Result of username availability check
/// </summary>
public class UsernameAvailabilityResult
{
    /// <summary>
    /// Whether username is available
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Reason if not available
    /// </summary>
    public UsernameUnavailableReason? Reason { get; init; }

    /// <summary>
    /// Username is available
    /// </summary>
    public static UsernameAvailabilityResult Available() =>
        new() { IsAvailable = true };

    /// <summary>
    /// Username is taken by existing user
    /// </summary>
    public static UsernameAvailabilityResult Taken() =>
        new() { IsAvailable = false, Reason = UsernameUnavailableReason.Taken };

    /// <summary>
    /// Username is reserved (used by former user)
    /// </summary>
    public static UsernameAvailabilityResult Reserved() =>
        new() { IsAvailable = false, Reason = UsernameUnavailableReason.Reserved };

    /// <summary>
    /// Username has invalid format
    /// </summary>
    public static UsernameAvailabilityResult InvalidFormat() =>
        new() { IsAvailable = false, Reason = UsernameUnavailableReason.InvalidFormat };
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
    /// Username is reserved (used by former user in username history)
    /// </summary>
    Reserved,

    /// <summary>
    /// Username format is invalid
    /// </summary>
    InvalidFormat
}
