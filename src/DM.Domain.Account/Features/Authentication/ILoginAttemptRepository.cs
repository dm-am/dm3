using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Repository for login attempt tracking
/// </summary>
public interface ILoginAttemptRepository
{
    /// <summary>
    /// Get the current failed attempt count for an account and address
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    /// <returns>Number of failed attempts</returns>
    Task<int> GetFailedAttemptCount(LoginAttemptOrigin origin);

    /// <summary>
    /// Get lockout information for an account and address
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    /// <returns>Lockout start time if locked, null otherwise</returns>
    Task<DateTime?> GetLockoutStart(LoginAttemptOrigin origin);

    /// <summary>
    /// Record a failed login attempt
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    /// <returns>New attempt count after recording</returns>
    Task<int> RecordFailedAttempt(LoginAttemptOrigin origin);

    /// <summary>
    /// Set lockout for an account and address
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    /// <param name="lockoutStart">Lockout start timestamp</param>
    Task SetLockout(LoginAttemptOrigin origin, DateTime lockoutStart);

    /// <summary>
    /// Reset attempts and lockout for one account and address pair (on an expired lockout)
    /// </summary>
    /// <param name="origin">Account and client address of the attempt</param>
    Task ResetAttempts(LoginAttemptOrigin origin);

    /// <summary>
    /// Reset attempts and lockout for an account, from every address (on successful login)
    /// </summary>
    /// <param name="email">User email</param>
    Task ResetAttempts(string email);
}
