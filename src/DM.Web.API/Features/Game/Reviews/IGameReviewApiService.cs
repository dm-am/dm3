using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// API service for game review operations
/// </summary>
public interface IGameReviewApiService
{
    /// <summary>
    /// Get reviews written about a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Paging parameters</param>
    Task<ListEnvelope<GameReviewDto>> GetList(Guid gameId, PagingQuery query);

    /// <summary>
    /// Get a single game review
    /// </summary>
    /// <param name="reviewId">Review identifier</param>
    Task<Envelope<GameReviewDto>> Get(Guid reviewId);

    /// <summary>
    /// Create a review for a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="request">Review data</param>
    Task<Envelope<GameReviewDto>> Create(Guid gameId, CreateGameReviewRequest request);
}
