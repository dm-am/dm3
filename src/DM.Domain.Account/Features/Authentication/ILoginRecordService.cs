using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Login records: what is written on every sign-in attempt, and what moderation
/// reads back out of them.
/// </summary>
/// <remarks>
/// The rules were in the HTTP layer. Which identity a failed attempt is filed
/// under, how much of a user agent is kept, and that a record which cannot be
/// written never fails the sign-in are decisions about the account rather than
/// about the response. Reading is here for the same reason: three API services
/// asked the repository directly, so each of them carried its own idea of how far
/// back a login history goes. The transport still supplies the address and the
/// user agent, because only it has them.
/// </remarks>
public interface ILoginRecordService
{
    /// <summary>
    /// Record a sign-in attempt
    /// </summary>
    /// <param name="userId">User the attempt authenticated, or null when it failed</param>
    /// <param name="identifier">Username or email the attempt was made with</param>
    /// <param name="ipAddress">Client address</param>
    /// <param name="userAgent">Client User-Agent header</param>
    /// <param name="isSuccessful">Whether the attempt succeeded</param>
    Task RecordAttempt(
        Guid? userId,
        string identifier,
        string ipAddress,
        string? userAgent,
        bool isSuccessful);

    /// <summary>
    /// Get the recent sign-ins of a user, successful and failed
    /// </summary>
    /// <param name="userId">User identifier</param>
    Task<IReadOnlyList<UserLoginRecord>> GetHistory(Guid userId);

    /// <summary>
    /// Get the addresses a user signed in from
    /// </summary>
    /// <param name="userId">User identifier</param>
    Task<IReadOnlyList<UserIpInfo>> GetIpAddresses(Guid userId);

    /// <summary>
    /// Get the users who share an address with this one
    /// </summary>
    /// <param name="userId">User identifier</param>
    Task<IReadOnlyList<LinkedProfile>> GetLinkedProfiles(Guid userId);
}
