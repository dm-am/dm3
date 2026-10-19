using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.Games;

public interface IGameRepository
{
    Task<int> Count(GamesQuery query, Guid userId, CancellationToken ct = default);

    Task<IEnumerable<Game>> GetGames(PagingData pagingData, GamesQuery query, Guid userId, CancellationToken ct = default);

    Task<IDictionary<Guid, IEnumerable<Guid>>> GetAvailableRoomIds(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default);

    Task<IDictionary<Guid, int>> GetTotalPostCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default);

    Task<IDictionary<Guid, int>> GetTotalCommentCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default);

    /// <inheritdoc cref="GetGame" path="/remarks" />
    Task<GameDetails?> GetGameDetails(
        Guid gameId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default);

    /// <remarks>
    /// The visibility scope answers for <paramref name="userId"/>. <paramref name="mayJudgePremoderation"/> opens
    /// the games awaiting a verdict, which have no curator in AwaitingEdits, and nothing else.
    /// </remarks>
    Task<Game?> GetGame(
        Guid gameId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default);

    /// <summary>
    /// Identifier of the game a public id addresses, or null when it addresses
    /// nothing the user may see.
    /// </summary>
    /// <inheritdoc cref="GetGame" path="/remarks" />
    Task<Guid?> FindGameIdByPublicId(
        string publicId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default);

    /// <inheritdoc cref="GetGame" path="/remarks" />
    Task<Game?> GetGameByPublicId(
        string publicId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default);

    /// <inheritdoc cref="GetGame" path="/remarks" />
    Task<GameDetails?> GetGameDetailsByPublicId(
        string publicId, Guid userId, bool mayJudgePremoderation, CancellationToken ct = default);

    /// <summary>
    /// For a premoderation verdict, ignoring the accessibility scope: a game in AwaitingEdits has no curator.
    /// </summary>
    /// <remarks>
    /// Only behind the Mentor+ gate; the viewer is passed for the viewer-scoped fields.
    /// </remarks>
    Task<GameDetails?> GetGameDetailsForModeration(Guid gameId, Guid viewerId, CancellationToken ct = default);

    /// <inheritdoc cref="GetGameDetailsForModeration" />
    Task<GameDetails?> GetGameDetailsByPublicIdForModeration(
        string publicId, Guid viewerId, CancellationToken ct = default);

    Task<IEnumerable<GameTag>> GetTags(CancellationToken ct = default);

    /// <remarks>
    /// The accessibility of <paramref name="userId" /> decides which ids resolve; the viewer-scoped fields
    /// are filled for <paramref name="viewerId" />, which differs only where the caller fixed visibility
    /// by other means (the rated-post feed).
    /// </remarks>
    Task<IEnumerable<Game>> GetByIds(
        IEnumerable<Guid> gameIds, Guid userId, Guid viewerId, CancellationToken ct = default);

    Task<GameDetails> Create(CreateGameEntity game, CreateRoomEntity room, CancellationToken ct = default);

    Task<GameDetails> Update(UpdateGameEntity updateGame, CancellationToken ct = default);

    /// <summary>
    /// Soft delete, dropping the subscriptions
    /// </summary>
    Task Delete(Guid gameId, Guid deletedByUserId, CancellationToken ct = default);
}
