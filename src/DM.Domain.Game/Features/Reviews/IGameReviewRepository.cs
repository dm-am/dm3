using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Reviews;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Reviews;

/// <summary>
/// Repository for game review operations
/// </summary>
public interface IGameReviewRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Get total count of reviews for a game
    /// </summary>
    /// <param name="gameId">Game ID</param>
    Task<int> CountAsync(Guid gameId);

    /// <summary>
    /// Get reviews for a game
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="paging">Paging parameters</param>
    Task<IEnumerable<Review>> GetAsync(Guid gameId, PagingData paging);

    /// <summary>
    /// Get single game review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    Task<Review?> GetAsync(Guid id);

    /// <summary>
    /// Get game review by author
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="authorId">Author ID</param>
    Task<Review?> GetByAuthorAsync(Guid gameId, Guid authorId);

    /// <summary>
    /// Get count of all game reviews with optional filter
    /// </summary>
    /// <param name="filter">Optional filter parameters</param>
    Task<int> CountAllAsync(GameReviewFilter? filter = null);

    /// <summary>
    /// Get all game reviews with optional filter
    /// </summary>
    /// <param name="paging">Paging parameters</param>
    /// <param name="filter">Optional filter parameters</param>
    Task<IEnumerable<Review>> GetAllAsync(PagingData paging, GameReviewFilter? filter = null);

    // ═══ WRITE ═══

    /// <summary>
    /// Check if user already has a review for the game
    /// </summary>
    /// <param name="authorId">Author ID</param>
    /// <param name="gameId">Game ID</param>
    Task<bool> ExistsAsync(Guid authorId, Guid gameId);

    /// <summary>
    /// Create new game review
    /// </summary>
    /// <param name="entity">Review data</param>
    Task<Review> CreateAsync(CreateGameReviewEntity entity);

    /// <summary>
    /// Update game review
    /// </summary>
    /// <param name="entity">Update data</param>
    Task<Review> UpdateAsync(UpdateGameReviewEntity entity);

    // ═══ ELIGIBILITY ═══

    /// <summary>
    /// Check if user can review a game (has at least one post in it)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="gameId">Game ID</param>
    Task<bool> CanReviewGameAsync(Guid userId, Guid gameId);

    /// <summary>
    /// Get user's total post count in games (for newbie check)
    /// </summary>
    /// <param name="userId">User ID</param>
    Task<int> GetUserPostCountAsync(Guid userId);
}
