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
    Task<IEnumerable<Game>> GetGames(PagingData pagingData, GamesQuery query, Guid userId, CancellationToken ct = default);

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
    Task<GameDetails?> GetGameDetails(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get game
    /// </summary>
    Task<Game?> GetGame(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Identifier of the game a public id addresses, or null when it addresses
    /// nothing the user may see.
    /// </summary>
    /// <remarks>
    /// For the callers that need the id and nothing else — every route that
    /// accepts either address form and then goes on to do something with the
    /// game. Applies the same visibility filter as the aggregate read.
    /// </remarks>
    Task<Guid?> FindGameIdByPublicId(string publicId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get game by public ID
    /// </summary>
    Task<Game?> GetGameByPublicId(string publicId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get game details by public ID
    /// </summary>
    Task<GameDetails?> GetGameDetailsByPublicId(string publicId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get all tags
    /// </summary>
    Task<IEnumerable<GameTag>> GetTags(CancellationToken ct = default);

    /// <summary>
    /// Get games by IDs
    /// </summary>
    /// <param name="gameIds">Game identifiers</param>
    /// <param name="userId">
    /// Whose accessibility decides which of the ids resolve: a game this user
    /// may not see is simply absent from the result.
    /// </param>
    /// <param name="viewerId">
    /// Who the viewer-scoped fields are filled for —
    /// <see cref="Game.IsViewerSubscriber" /> and nothing else today. The same
    /// as <paramref name="userId" /> for an ordinary read; the two differ only
    /// where the caller has already fixed visibility by other means and wants
    /// the accessibility filter to stay out of it, which is the rated-post feed.
    /// Two parameters rather than one so that a caller in that position does not
    /// have to re-query the subscriptions this method already reads.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Game>> GetByIds(
        IEnumerable<Guid> gameIds, Guid userId, Guid viewerId, CancellationToken ct = default);

    // === WRITE ===

    /// <summary>
    /// Create game with initial room
    /// </summary>
    Task<GameDetails> Create(CreateGameEntity game, CreateRoomEntity room, CancellationToken ct = default);

    /// <summary>
    /// Update game
    /// </summary>
    Task<GameDetails> Update(UpdateGameEntity updateGame, CancellationToken ct = default);

    /// <summary>
    /// Delete game (soft delete)
    /// </summary>
    Task Delete(Guid gameId, CancellationToken ct = default);
}
