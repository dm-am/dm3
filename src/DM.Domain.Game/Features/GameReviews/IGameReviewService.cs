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
    /// Whether the current user may review the game: true exactly when
    /// <see cref="CreateAsync" /> takes a review of it, the text aside. The page
    /// offers the form by this answer instead of refusing what was typed.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    Task<bool> MayCreateAsync(Guid gameId);

    /// <summary>
    /// One review of the game, read inside the game: a game the reader may not
    /// open answers as a game that is not there, and a review of another game
    /// as a review that is not there
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="reviewId">Review ID</param>
    /// <returns>Review or throws if not found</returns>
    Task<GameReview> GetAsync(Guid gameId, Guid reviewId);

    /// <summary>
    /// Get reviews for a game the reader may open
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetListAsync(Guid gameId, PagingQuery query);

    /// <summary>
    /// Get the reviews of the games the reader may open, with optional filtering
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
