using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Repository for login attempt tracking (MongoDB-backed for cluster support)
/// </summary>
public interface ILoginAttemptRepository
{
    /// <summary>
    /// Get the current failed attempt count for an email
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>Number of failed attempts</returns>
    Task<int> GetFailedAttemptCount(string email);

    /// <summary>
    /// Get lockout information for an email
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>Lockout start time if locked, null otherwise</returns>
    Task<DateTime?> GetLockoutStart(string email);

    /// <summary>
    /// Record a failed login attempt
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>New attempt count after recording</returns>
    Task<int> RecordFailedAttempt(string email);

    /// <summary>
    /// Set account lockout
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="lockoutStart">Lockout start timestamp</param>
    Task SetLockout(string email, DateTime lockoutStart);

    /// <summary>
    /// Reset all attempts and lockout for an email (on successful login)
    /// </summary>
    /// <param name="email">User email</param>
    Task ResetAttempts(string email);

    /// <summary>
    /// Clean up expired attempt records
    /// </summary>
    /// <param name="expirationHours">Hours after which records are considered expired</param>
    Task CleanupExpiredRecords(int expirationHours);
}
