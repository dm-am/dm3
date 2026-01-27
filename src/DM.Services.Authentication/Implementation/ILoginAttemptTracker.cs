using System;
using System.Threading.Tasks;

namespace DM.Services.Authentication.Implementation;

/// <summary>
/// Service for tracking login attempts and implementing progressive delays
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
