using System;
using System.Threading.Tasks;

namespace DM.Services.Authentication.Implementation;

/// <summary>
/// Service for tracking login attempts and implementing progressive delays and account lockout
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>
    /// Get the delay that should be applied before allowing a login attempt
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>Delay in seconds</returns>
    Task<int> GetDelayForUser(string login);

    /// <summary>
    /// Check if account is locked due to too many failed attempts
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>True if account is locked, false otherwise</returns>
    Task<bool> IsAccountLocked(string login);

    /// <summary>
    /// Get the remaining lockout time in seconds
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>Remaining lockout time in seconds, 0 if not locked</returns>
    Task<int> GetRemainingLockoutSeconds(string login);

    /// <summary>
    /// Record a failed login attempt
    /// </summary>
    /// <param name="login">User login</param>
    Task RecordFailedAttempt(string login);

    /// <summary>
    /// Reset login attempts for a user (called on successful login)
    /// </summary>
    /// <param name="login">User login</param>
    Task ResetAttempts(string login);
}
