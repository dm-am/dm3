using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Users;

/// <summary>
/// Minimal user lookup service for cross-module operations.
/// Used by Blog, Game, Moderation modules to resolve users without importing Domain.Personal.
/// </summary>
/// <remarks>
/// Full implementation: Domain.Personal/Features/Profiles/IUserService
/// </remarks>
public interface IUserLookupService
{
    /// <summary>
    /// Get user by username (throws HttpException if not found)
    /// </summary>
    /// <param name="username">Username to find</param>
    /// <returns>User info</returns>
    /// <exception cref="DM.Domain.Core.Exceptions.HttpException">User not found (410 Gone)</exception>
    Task<GeneralUser> Get(string username);

    /// <summary>
    /// Get user by ID (throws HttpException if not found)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>User info</returns>
    /// <exception cref="DM.Domain.Core.Exceptions.HttpException">User not found (410 Gone)</exception>
    Task<GeneralUser> Get(Guid userId);

    /// <summary>
    /// Check if username exists (for validators)
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if user exists</returns>
    Task<bool> UsernameExists(string username, CancellationToken ct = default);

    /// <summary>
    /// Check if user exists by username (alias for UsernameExists, for validator compatibility)
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if user exists</returns>
    Task<bool> UserExists(string username, CancellationToken ct = default);

    /// <summary>
    /// Find user ID by username (for game/blog resolvers)
    /// </summary>
    /// <param name="username">Username to find</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple with Found flag and UserId (Empty if not found)</returns>
    Task<(bool Found, Guid UserId)> FindUserId(string username, CancellationToken ct = default);
}
