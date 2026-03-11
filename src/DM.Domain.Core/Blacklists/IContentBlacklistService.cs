using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Blacklists;

/// <summary>
/// Base interface for content blacklist (Game, Blog)
/// </summary>
/// <remarks>
/// Provides common blacklist operations for content entities.
/// Implementations: IGameBlacklistService, IBlogBlacklistService
/// </remarks>
public interface IContentBlacklistService
{
    /// <summary>
    /// Get list of blacklisted users for the entity
    /// </summary>
    /// <param name="entityId">Entity identifier (game or blog)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of blacklisted users</returns>
    Task<IEnumerable<GeneralUser>> GetBlacklistAsync(Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Add user to entity blacklist
    /// </summary>
    /// <param name="entityId">Entity identifier (game or blog)</param>
    /// <param name="username">Username to blacklist</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Blacklisted user info</returns>
    Task<GeneralUser> AddToBlacklistAsync(Guid entityId, string username, CancellationToken ct = default);

    /// <summary>
    /// Remove user from entity blacklist
    /// </summary>
    /// <param name="entityId">Entity identifier (game or blog)</param>
    /// <param name="username">Username to remove</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveFromBlacklistAsync(Guid entityId, string username, CancellationToken ct = default);

    /// <summary>
    /// Check if user is blocked from entity
    /// </summary>
    /// <param name="entityId">Entity identifier (game or blog)</param>
    /// <param name="userId">User identifier to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if user is blocked</returns>
    Task<bool> IsBlockedAsync(Guid entityId, Guid userId, CancellationToken ct = default);
}
