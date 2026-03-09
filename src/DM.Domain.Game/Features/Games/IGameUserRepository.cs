using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Repository for game user operations (players, assistants, readers)
/// </summary>
public interface IGameUserRepository
{
    #region Players

    /// <summary>
    /// Get all players in a game
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetPlayers(Guid gameId);

    /// <summary>
    /// Check if user is a player in game
    /// </summary>
    Task<bool> IsPlayer(Guid gameId, string username);

    /// <summary>
    /// Exile player from game
    /// </summary>
    Task<IEnumerable<Guid>> ExilePlayer(Guid gameId, string username);

    /// <summary>
    /// Mark characters as left when player leaves
    /// </summary>
    Task<int> MarkCharactersAsLeft(Guid userId, Guid gameId);

    #endregion

    #region Assistants

    /// <summary>
    /// Get all assistants in a game
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetAssistants(Guid gameId);

    /// <summary>
    /// Check if user is an assistant by username
    /// </summary>
    Task<bool> IsAssistantByUsername(Guid gameId, string username);

    /// <summary>
    /// Check if user is an assistant by user ID
    /// </summary>
    Task<bool> IsAssistantByUserId(Guid userId, Guid gameId);

    /// <summary>
    /// Remove assistant by username
    /// </summary>
    Task RemoveAssistantByUsername(Guid gameId, string username);

    /// <summary>
    /// Remove assistant by user ID
    /// </summary>
    Task RemoveAssistantByUserId(Guid userId, Guid gameId);

    #endregion

    #region Readers

    /// <summary>
    /// Check if user is a reader
    /// </summary>
    Task<bool> IsReader(Guid userId, Guid gameId);

    /// <summary>
    /// Remove reader subscription
    /// </summary>
    Task RemoveReader(Guid userId, Guid gameId);

    #endregion
}
