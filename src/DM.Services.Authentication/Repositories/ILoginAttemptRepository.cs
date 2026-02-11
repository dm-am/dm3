using System;
using System.Threading.Tasks;

namespace DM.Services.Authentication.Repositories;

/// <summary>
/// Repository for login attempt tracking (MongoDB-backed for cluster support)
/// </summary>
public interface ILoginAttemptRepository
{
    /// <summary>
    /// Get the current failed attempt count for a login
    /// </summary>
    /// <param name="login">User login (email)</param>
    /// <returns>Number of failed attempts</returns>
    Task<int> GetFailedAttemptCount(string login);

    /// <summary>
    /// Get lockout information for a login
    /// </summary>
    /// <param name="login">User login (email)</param>
    /// <returns>Lockout start time if locked, null otherwise</returns>
    Task<DateTime?> GetLockoutStart(string login);

    /// <summary>
    /// Record a failed login attempt
    /// </summary>
    /// <param name="login">User login (email)</param>
    /// <returns>New attempt count after recording</returns>
    Task<int> RecordFailedAttempt(string login);

    /// <summary>
    /// Set account lockout
    /// </summary>
    /// <param name="login">User login (email)</param>
    /// <param name="lockoutStart">Lockout start timestamp</param>
    Task SetLockout(string login, DateTime lockoutStart);

    /// <summary>
    /// Reset all attempts and lockout for a login (on successful login)
    /// </summary>
    /// <param name="login">User login (email)</param>
    Task ResetAttempts(string login);

    /// <summary>
    /// Clean up expired attempt records
    /// </summary>
    /// <param name="expirationHours">Hours after which records are considered expired</param>
    Task CleanupExpiredRecords(int expirationHours);
}
