using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Authentication information storage
/// </summary>
public interface IAuthenticationRepository
{
    /// <summary>
    /// Search for user by its email
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>Pair of operation success flag and the user data. If no user is found, the user will be null</returns>
    Task<(bool Success, AuthenticatedUser? User)> TryFindUserByEmail(string email);

    /// <summary>
    /// Search for user by its id
    /// </summary>
    /// <param name="userId">User id</param>
    /// <returns>User data, or null if not found</returns>
    Task<AuthenticatedUser?> FindUser(Guid userId);

    /// <summary>
    /// Search for an authentication session owned by the given user
    /// </summary>
    /// <remarks>
    /// The session must belong to <paramref name="userId" />. A session id that
    /// exists but belongs to somebody else resolves to null: both halves of the
    /// token have to agree.
    /// </remarks>
    /// <param name="userId">Owning user id</param>
    /// <param name="sessionId">Authentication session id</param>
    /// <returns>Session, or null if the user has no such session</returns>
    Task<Session?> FindUserSession(Guid userId, Guid sessionId);

    /// <summary>
    /// Search for user settings by user id
    /// </summary>
    /// <param name="userId">User id</param>
    /// <returns>User settings</returns>
    Task<UserSettings> FindUserSettings(Guid userId);

    /// <summary>
    /// Remove authentication session
    /// </summary>
    /// <param name="userId">Authenticated user id</param>
    /// <param name="sessionId">Authentication session id</param>
    Task RemoveSession(Guid userId, Guid sessionId);

    /// <summary>
    /// Update authentication session that is about to expire
    /// </summary>
    /// <param name="userId">Authenticated user id</param>
    /// <param name="sessionId">Authentication session id</param>
    /// <param name="expirationDate">New expiration date</param>
    Task RefreshSession(Guid userId, Guid sessionId, DateTimeOffset expirationDate);

    /// <summary>
    /// Append new session to user authentication sessions
    /// </summary>
    /// <param name="userId">Authenticated user id</param>
    /// <param name="session">Session data for creating new session</param>
    /// <returns>Created session</returns>
    Task<Session> AddSession(Guid userId, CreateSession session);

    /// <summary>
    /// Remove all sessions from the user except one
    /// </summary>
    /// <param name="userId">Authenticated user id</param>
    /// <param name="sessionId">Session id</param>
    Task RemoveSessionsExcept(Guid userId, Guid sessionId);

    /// <summary>
    /// Update user last activity date
    /// </summary>
    /// <param name="userId">User id</param>
    /// <param name="lastActivityUtc">Last activity timestamp</param>
    Task UpdateActivity(Guid userId, DateTimeOffset lastActivityUtc);

    /// <summary>
    /// Get all active sessions for user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="currentSessionId">Current session ID (to mark as current)</param>
    /// <returns>List of active sessions</returns>
    Task<IReadOnlyCollection<Session>> GetUserSessions(Guid userId, Guid? currentSessionId = null);

    /// <summary>
    /// Check if email exists in PendingRegistrations (registration not completed)
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>True if pending registration exists</returns>
    Task<bool> IsPendingRegistration(string email);

    /// <summary>
    /// Remove all sessions for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    Task RemoveAllSessions(Guid userId);
}
