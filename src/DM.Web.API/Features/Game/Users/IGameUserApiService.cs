using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Game.Users;

/// <summary>
/// API service for game users
/// </summary>
public interface IGameUserApiService
{
    #region Users

    /// <summary>
    /// Get all users of a game, optionally filtered by role
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="role">Optional role filter (master, assistant, mentor, player, applicant, formerPlayer, reader)</param>
    Task<IEnumerable<GameUser>> GetUsers(Guid gameId, GameRole? role = null);

    /// <summary>
    /// Remove a user from a game by user ID
    /// </summary>
    Task RemoveUser(Guid gameId, Guid userId);

    #endregion

    #region Assistants

    /// <summary>
    /// Get assistants only
    /// </summary>
    Task<IEnumerable<GameUser>> GetAssistants(Guid gameId);

    /// <summary>
    /// Remove assistant by username
    /// </summary>
    Task RemoveAssistantByUsername(Guid gameId, string username);

    #endregion

    #region Readers

    /// <summary>
    /// Get readers only (subscribed users without other roles)
    /// </summary>
    Task<IEnumerable<GameUser>> GetReaders(Guid gameId);

    /// <summary>
    /// Subscribe current user as reader
    /// </summary>
    Task<GameUser> Subscribe(Guid gameId);

    /// <summary>
    /// Unsubscribe current user
    /// </summary>
    Task Unsubscribe(Guid gameId);

    // Note: RemoveReader is not provided - readers can only unsubscribe themselves
    // Use blacklist to prevent problematic users from accessing content

    #endregion
}
