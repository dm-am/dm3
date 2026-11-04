using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Sessions of an account: ending the current one, the others, one by its id,
/// or all of them.
/// </summary>
/// <remarks>
/// Nothing here builds an identity. Building one is authentication's, so that
/// the bans in force fold into it at a single point; Logout answers with the
/// guest and LogoutElsewhere with the identity already in hand.
/// </remarks>
public interface ISessionService
{
    /// <summary>
    /// Logout as a current user
    /// </summary>
    /// <returns>Guest identity</returns>
    Task<IIdentity> Logout();

    /// <summary>
    /// Logout from all devices except this
    /// </summary>
    /// <returns>The identity in hand: the current session is the one kept, not reissued</returns>
    Task<IIdentity> LogoutElsewhere();

    /// <summary>
    /// Get all active sessions for current user
    /// </summary>
    /// <returns>Collection of user sessions</returns>
    Task<IReadOnlyCollection<Session>> GetCurrentUserSessions();

    /// <summary>
    /// Terminate a specific session for a user
    /// </summary>
    /// <param name="userId">User ID who owns the session</param>
    /// <param name="sessionId">Session ID to terminate</param>
    Task TerminateSession(Guid userId, Guid sessionId);

    /// <summary>
    /// Logout from all devices for a specific user (used for password reset)
    /// </summary>
    /// <param name="userId">User identifier</param>
    Task LogoutAll(Guid userId);
}
