using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Unified service for game CRUD operations
/// </summary>
public interface IGameService
{
    #region Create

    /// <summary>
    /// Create new game
    /// </summary>
    Task<GameExtended> CreateAsync(CreateGame createGame);

    #endregion

    #region Read

    /// <summary>
    /// Get available game tags
    /// </summary>
    Task<IEnumerable<GameTag>> GetTagsAsync();

    /// <summary>
    /// Get user's own games
    /// </summary>
    Task<IEnumerable<GameModel>> GetOwnGamesAsync();

    /// <summary>
    /// Get games page with filtering
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of fetched games and paging data</returns>
    Task<(IEnumerable<GameModel> games, PagingResult paging)> GetGamesAsync(GamesQuery query);

    /// <summary>
    /// Get game by identifier
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task<GameModel> GetAsync(Guid gameId);

    /// <summary>
    /// Get game details by identifier
    /// </summary>
    Task<GameExtended> GetDetailsAsync(Guid gameId);

    /// <summary>
    /// Get popular games
    /// </summary>
    Task<IEnumerable<GameModel>> GetPopularAsync();

    /// <summary>
    /// Get games by IDs (for subscribed games)
    /// </summary>
    /// <param name="gameIds">Game identifiers</param>
    Task<IEnumerable<GameModel>> GetSubscribedAsync(IEnumerable<Guid> gameIds);

    #endregion

    #region Update

    /// <summary>
    /// Update existing game
    /// </summary>
    /// <param name="updateGame">Update game model</param>
    Task<GameExtended> UpdateAsync(UpdateGame updateGame);

    #endregion

    #region Delete

    /// <summary>
    /// Remove existing game (soft delete)
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task DeleteAsync(Guid gameId);

    #endregion

    #region Users

    /// <summary>
    /// Get list of game players (users with active characters)
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetPlayersAsync(Guid gameId);

    /// <summary>
    /// Remove player from game (exile all their characters)
    /// </summary>
    Task RemovePlayerAsync(Guid gameId, string username);

    /// <summary>
    /// Get list of game assistants
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetAssistantsAsync(Guid gameId);

    /// <summary>
    /// Remove assistant from game
    /// </summary>
    Task RemoveAssistantAsync(Guid gameId, string username);

    /// <summary>
    /// Leave a game (as reader, player, or assistant)
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task LeaveAsync(Guid gameId);

    #endregion
}
