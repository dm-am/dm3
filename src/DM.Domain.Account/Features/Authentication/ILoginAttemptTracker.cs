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
    /// <param name="origin">Account and client address of the attempt</param>
    /// <returns>Delay in seconds</returns>
    Task<int> GetDelayForUser(LoginAttemptOrigin origin);

    /// <summary>
    /// Check if the attempt is locked out due to too many failed attempts
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    /// <returns>True if locked out, false otherwise</returns>
    Task<bool> IsAccountLocked(LoginAttemptOrigin origin);

    /// <summary>
    /// Get the remaining lockout time in seconds
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    /// <returns>Remaining lockout time in seconds, 0 if not locked</returns>
    Task<int> GetRemainingLockoutSeconds(LoginAttemptOrigin origin);

    /// <summary>
    /// Record a failed login attempt
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    Task RecordFailedAttempt(LoginAttemptOrigin origin);

    /// <summary>
    /// Reset login attempts for an account, from every address (called on successful login)
    /// </summary>
    /// <param name="email">User email</param>
    Task ResetAttempts(string email);
}
