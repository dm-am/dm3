using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Repository for game operations
/// </summary>
public interface IGameRepository
{
    // === READ ===

    /// <summary>
    /// Count games matching query
    /// </summary>
    Task<int> Count(GamesQuery query, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get games with paging
    /// </summary>
    Task<IEnumerable<GameModel>> GetGames(PagingData pagingData, GamesQuery query, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get user's own games (master, assistant, player, reader)
    /// </summary>
    Task<IEnumerable<GameModel>> GetOwn(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get available room IDs for games
    /// </summary>
    Task<IDictionary<Guid, IEnumerable<Guid>>> GetAvailableRoomIds(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get post pendencies for games
    /// </summary>
    Task<IEnumerable<PostPendency>> GetPostPendencies(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get rooms and post pendencies for games
    /// </summary>
    Task<(IDictionary<Guid, IEnumerable<Guid>> rooms, IEnumerable<PostPendency> postPendencies)> GetRoomsAndPostPendencies(
        IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get total post counts for games
    /// </summary>
    Task<IDictionary<Guid, int>> GetTotalPostCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default);

    /// <summary>
    /// Get total comment counts for games
    /// </summary>
    Task<IDictionary<Guid, int>> GetTotalCommentCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default);

    /// <summary>
    /// Get game details
    /// </summary>
    Task<GameExtended?> GetGameDetails(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get game
    /// </summary>
    Task<GameModel?> GetGame(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get all tags
    /// </summary>
    Task<IEnumerable<GameTag>> GetTags(CancellationToken ct = default);

    /// <summary>
    /// Get popular games by subscriber count
    /// </summary>
    Task<IEnumerable<GameModel>> GetPopularGames(int gamesCount, CancellationToken ct = default);

    /// <summary>
    /// Get games by IDs
    /// </summary>
    Task<IEnumerable<GameModel>> GetByIds(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default);

    // === WRITE ===

    /// <summary>
    /// Create game with initial room
    /// </summary>
    Task<GameExtended> Create(CreateGameEntity game, CreateRoomEntity room, CancellationToken ct = default);

    /// <summary>
    /// Update game
    /// </summary>
    Task<GameExtended> Update(UpdateGameEntity updateGame, CancellationToken ct = default);

    /// <summary>
    /// Delete game (soft delete)
    /// </summary>
    Task Delete(Guid gameId, CancellationToken ct = default);
}
