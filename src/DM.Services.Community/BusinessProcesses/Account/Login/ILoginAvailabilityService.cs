using System.Threading;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.Login;

/// <summary>
/// Service for checking login availability
/// </summary>
public interface ILoginAvailabilityService
{
    /// <summary>
    /// Check if login is available for registration
    /// </summary>
    /// <param name="login">Login to check</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Availability result</returns>
    Task<LoginAvailabilityResult> CheckAvailability(string login, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of login availability check
/// </summary>
public class LoginAvailabilityResult
{
    /// <summary>
    /// Whether login is available
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Reason if not available: "taken", "reserved", "invalid_format"
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Login is available
    /// </summary>
    public static LoginAvailabilityResult Available() =>
        new() { IsAvailable = true };

    /// <summary>
    /// Login is taken by existing user
    /// </summary>
    public static LoginAvailabilityResult Taken() =>
        new() { IsAvailable = false, Reason = "taken" };

    /// <summary>
    /// Login is reserved (used by former user)
    /// </summary>
    public static LoginAvailabilityResult Reserved() =>
        new() { IsAvailable = false, Reason = "reserved" };

    /// <summary>
    /// Login has invalid format
    /// </summary>
    public static LoginAvailabilityResult InvalidFormat() =>
        new() { IsAvailable = false, Reason = "invalid_format" };
}
