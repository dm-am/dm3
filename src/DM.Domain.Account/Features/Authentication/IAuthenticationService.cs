using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// User authentication service
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticate via email credentials
    /// </summary>
    /// <param name="email">User email address</param>
    /// <param name="password">User password</param>
    /// <param name="rememberMe">If true, session is persistent (30 days); otherwise 24 hours</param>
    /// <param name="context">Session context with device info</param>
    /// <returns>Authentication identity</returns>
    Task<IIdentity> Authenticate(string email, string password, bool rememberMe = true, SessionContext? context = null);

    /// <summary>
    /// Authenticate via token credentials
    /// </summary>
    /// <param name="authToken">Authentication token</param>
    /// <returns>Authentication identity</returns>
    Task<IIdentity> Authenticate(string authToken);

    /// <summary>
    /// Authenticate unconditionally
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Authentication identity</returns>
    Task<IIdentity> Authenticate(Guid userId);

    /// <summary>
    /// Logout as a current user
    /// </summary>
    /// <returns>Guest identity</returns>
    Task<IIdentity> Logout();

    /// <summary>
    /// Logout from all devices except this
    /// </summary>
    /// <returns>Newly created authentication identity</returns>
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
