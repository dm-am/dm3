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
    /// Username is held, by a live user or by a reservation
    /// </summary>
    public static UsernameAvailabilityResult Taken() =>
        new() { IsAvailable = false, Reason = UsernameUnavailableReason.Taken };
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
    /// Username is held: by a user, by a former user, or by a name somebody
    /// once wore. One reason for all three, because telling them apart tells
    /// a guest who was here and left (SECURITY.md).
    /// </summary>
    Taken,
    /// <summary>
    /// Username format is invalid
    /// </summary>
    InvalidFormat
}
