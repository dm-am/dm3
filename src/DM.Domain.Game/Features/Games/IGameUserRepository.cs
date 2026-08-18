using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Repository for game user operations (players and assistants)
/// </summary>
public interface IGameUserRepository
{
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
    /// Remove assistant by username
    /// </summary>
    Task RemoveAssistantByUsername(Guid gameId, string username);

    #endregion
}
