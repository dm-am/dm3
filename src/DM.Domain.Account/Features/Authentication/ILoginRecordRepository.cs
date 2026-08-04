using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Repository for user login records (IP tracking for moderation)
/// </summary>
public interface ILoginRecordRepository
{
    /// <summary>
    /// Record a login attempt (successful or failed)
    /// </summary>
    /// <param name="record">Login record to save</param>
    Task Record(UserLoginRecord record);

    /// <summary>
    /// Try to resolve a user ID from identifier (username or email, case-insensitive).
    /// Used to record failed login attempts where the identity is not yet established.
    /// </summary>
    /// <param name="identifier">User identifier (username or email)</param>
    /// <returns>User ID if found, null otherwise</returns>
    Task<Guid?> TryResolveUserId(string identifier);

    /// <summary>
    /// Get unique IP addresses for a user (successful logins only)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="days">Number of days to look back</param>
    /// <returns>Aggregated IP info sorted by last seen descending</returns>
    Task<IReadOnlyList<UserIpInfo>> GetUserIps(Guid userId, int days = 365);

    /// <summary>
    /// Get users who share IP addresses with the target user (successful logins only)
    /// </summary>
    /// <param name="userId">Target user identifier</param>
    /// <param name="days">Number of days to look back</param>
    /// <returns>Linked profiles sorted by shared IP count descending</returns>
    Task<IReadOnlyList<LinkedProfile>> GetLinkedProfiles(Guid userId, int days = 365);

    /// <summary>
    /// Get recent login history for a user (both successful and failed)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take</param>
    /// <returns>Login records sorted by date descending</returns>
    Task<IReadOnlyList<UserLoginRecord>> GetLoginHistory(Guid userId, int skip, int take);

    /// <summary>
    /// Count the login records of a user
    /// </summary>
    Task<int> CountLoginHistory(Guid userId);

    /// <summary>
    /// Check if user has previously logged in from this IP address
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="ipAddress">IP address to check</param>
    /// <returns>True if this IP was used for successful login before</returns>
    Task<bool> HasLoginFromIp(Guid userId, string ipAddress);
}
