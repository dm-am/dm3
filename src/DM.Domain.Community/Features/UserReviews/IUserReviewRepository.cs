using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Reviews;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// Repository for user review operations
/// </summary>
public interface IUserReviewRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Get total count of reviews for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    Task<int> CountAsync(Guid targetUserId);

    /// <summary>
    /// Get reviews for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="paging">Paging parameters</param>
    Task<IEnumerable<Review>> GetAsync(Guid targetUserId, PagingData paging);

    /// <summary>
    /// Get single user review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    Task<Review?> GetAsync(Guid id);

    /// <summary>
    /// Get user review by author
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="authorId">Author ID</param>
    Task<Review?> GetByAuthorAsync(Guid targetUserId, Guid authorId);

    /// <summary>
    /// Get count of all user reviews with optional filter
    /// </summary>
    /// <param name="filter">Optional filter parameters</param>
    Task<int> CountAllAsync(UserReviewFilter? filter = null);

    /// <summary>
    /// Get all user reviews with optional filter
    /// </summary>
    /// <param name="paging">Paging parameters</param>
    /// <param name="filter">Optional filter parameters</param>
    Task<IEnumerable<Review>> GetAllAsync(PagingData paging, UserReviewFilter? filter = null);

    // ═══ WRITE ═══

    /// <summary>
    /// Check if user already has a review for target user
    /// </summary>
    /// <param name="authorId">Author ID</param>
    /// <param name="targetUserId">Target user ID</param>
    Task<bool> ExistsAsync(Guid authorId, Guid targetUserId);

    /// <summary>
    /// Create new user review
    /// </summary>
    /// <param name="entity">Review data</param>
    Task<Review> CreateAsync(CreateUserReviewEntity entity);

    /// <summary>
    /// Update user review
    /// </summary>
    /// <param name="entity">Update data</param>
    Task<Review> UpdateAsync(UpdateUserReviewEntity entity);

    // ═══ ELIGIBILITY ═══

    /// <summary>
    /// Check if two users have played together in the same game
    /// </summary>
    /// <param name="userId1">First user ID</param>
    /// <param name="userId2">Second user ID</param>
    Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2);

    /// <summary>
    /// Get user's total post count in games
    /// </summary>
    /// <param name="userId">User ID</param>
    Task<int> GetUserPostCountAsync(Guid userId);
}
