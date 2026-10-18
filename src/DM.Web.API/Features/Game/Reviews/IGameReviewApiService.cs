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
    /// Get game reviews received by a user: reviews of the games they master
    /// </summary>
    /// <param name="username">Game master's username</param>
    /// <param name="query">Paging, search and sorting</param>
    Task<ListEnvelope<GameReviewDto>> GetReceivedByUser(string username, UserGameReviewsQuery query);

    /// <summary>
    /// Get game reviews written by a user
    /// </summary>
    /// <param name="username">Review author's username</param>
    /// <param name="query">Paging, search and sorting</param>
    Task<ListEnvelope<GameReviewDto>> GetWrittenByUser(string username, UserGameReviewsQuery query);

    /// <summary>
    /// Get a single review of the game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="reviewId">Review identifier</param>
    Task<Envelope<GameReviewDto>> Get(Guid gameId, Guid reviewId);

    /// <summary>
    /// Create a review for a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="request">Review data</param>
    Task<Envelope<GameReviewDto>> Create(Guid gameId, CreateGameReviewRequest request);

    /// <summary>
    /// Update a game review the caller authored
    /// </summary>
    /// <param name="reviewId">Review identifier</param>
    /// <param name="request">Fields to change</param>
    Task<Envelope<GameReviewDto>> Update(Guid reviewId, UpdateGameReviewRequest request);

    /// <summary>
    /// Delete a game review: its author, or a senior moderator
    /// </summary>
    /// <param name="reviewId">Review identifier</param>
    Task Delete(Guid reviewId);
}
