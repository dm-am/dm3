using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.GameReviews;

/// <summary>
/// Service for game review operations
/// </summary>
public interface IGameReviewService
{
    /// <summary>
    /// Create new game review
    /// </summary>
    /// <param name="createReview">Review data</param>
    /// <returns>Created review</returns>
    Task<GameReview> CreateAsync(CreateGameReview createReview);

    /// <summary>
    /// Get single review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    /// <returns>Review or throws if not found</returns>
    Task<GameReview> GetAsync(Guid id);

    /// <summary>
    /// Get reviews for a game
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetListAsync(Guid gameId, PagingQuery query);

    /// <summary>
    /// Get all game reviews with optional filtering
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Optional filter parameters</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetAllAsync(PagingQuery query, GameReviewFilter? filter = null);

    /// <summary>
    /// Get game review by author (check if author already reviewed game)
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="authorId">Review author ID</param>
    /// <returns>Review or null if not found</returns>
    Task<GameReview?> GetByAuthorAsync(Guid gameId, Guid authorId);

    /// <summary>
    /// Update game review
    /// </summary>
    /// <param name="updateReview">Update data</param>
    /// <returns>Updated review</returns>
    Task<GameReview> UpdateAsync(UpdateGameReview updateReview);

    /// <summary>
    /// Delete game review
    /// </summary>
    /// <param name="id">Review ID</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Check if user already has a review for the game
    /// </summary>
    /// <param name="authorId">Review author ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>True if review already exists</returns>
    Task<bool> ExistsAsync(Guid authorId, Guid gameId);

    /// <summary>
    /// Check if review can be edited (within 24 hours of creation)
    /// </summary>
    /// <param name="review">Review to check</param>
    /// <returns>True if review can still be edited</returns>
    bool CanEdit(GameReview review);
}
