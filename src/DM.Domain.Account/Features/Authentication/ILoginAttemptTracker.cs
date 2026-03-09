using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Service for tracking login attempts and implementing progressive delays and account lockout
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>
    /// Get the delay that should be applied before allowing a login attempt
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>Delay in seconds</returns>
    Task<int> GetDelayForUser(string email);

    /// <summary>
    /// Check if account is locked due to too many failed attempts
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>True if account is locked, false otherwise</returns>
    Task<bool> IsAccountLocked(string email);

    /// <summary>
    /// Get the remaining lockout time in seconds
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>Remaining lockout time in seconds, 0 if not locked</returns>
    Task<int> GetRemainingLockoutSeconds(string email);

    /// <summary>
    /// Record a failed login attempt
    /// </summary>
    /// <param name="email">User email</param>
    Task RecordFailedAttempt(string email);

    /// <summary>
    /// Reset login attempts for a user (called on successful login)
    /// </summary>
    /// <param name="email">User email</param>
    Task ResetAttempts(string email);
}
