using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Games.Reading;

/// <summary>
/// Game reading storage
/// </summary>
internal interface IGameReadingRepository
{
    /// <summary>
    /// Count games that match the query
    /// </summary>
    /// <param name="status">Status filter</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<int> Count(GamesQuery status, Guid userId);

    /// <summary>
    /// Get list of matching games on certain page
    /// </summary>
    /// <param name="pagingData">Paging data</param>
    /// <param name="query">Filter query</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<IEnumerable<Dto.Output.Game>> GetGames(PagingData pagingData, GamesQuery query, Guid userId);

    /// <summary>
    /// Get list of user owned active games
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<IEnumerable<Dto.Output.Game>> GetOwn(Guid userId);

    /// <summary>
    /// Get available room identifiers grouped by game ids
    /// </summary>
    /// <returns></returns>
    Task<IDictionary<Guid, IEnumerable<Guid>>> GetAvailableRoomIds(IEnumerable<Guid> gameIds, Guid userId);

    /// <summary>
    /// Get game post pendencies
    /// </summary>
    /// <param name="gameIds">Game identifiers</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<IEnumerable<PostPendency>> GetPostPendencies(IEnumerable<Guid> gameIds, Guid userId);

    /// <summary>
    /// Get single game model
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<Dto.Output.Game?> GetGame(Guid gameId, Guid userId);

    /// <summary>
    /// Get single game full info
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<GameExtended?> GetGameDetails(Guid gameId, Guid userId);

    /// <summary>
    /// Get list of available tags
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<GameTag>> GetTags();
        
    /// <summary>
    /// Get list of popular active games
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<Dto.Output.Game>> GetPopularGames(int gamesCount);

    /// <summary>
    /// Get rooms and post pendencies in one query (optimization)
    /// </summary>
    Task<(IDictionary<Guid, IEnumerable<Guid>> rooms, IEnumerable<PostPendency> postPendencies)> GetRoomsAndPostPendencies(
        IEnumerable<Guid> gameIds, Guid userId);

    /// <summary>
    /// Get total post counts per game (for anonymous users)
    /// </summary>
    Task<IDictionary<Guid, int>> GetTotalPostCounts(IEnumerable<Guid> gameIds);

    /// <summary>
    /// Get total comment counts per game (for anonymous users)
    /// </summary>
    Task<IDictionary<Guid, int>> GetTotalCommentCounts(IEnumerable<Guid> gameIds);
}