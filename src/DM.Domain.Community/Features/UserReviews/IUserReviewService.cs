using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Reviews;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// Service for user review operations
/// </summary>
public interface IUserReviewService
{
    /// <summary>
    /// Create new user review
    /// </summary>
    /// <param name="createReview">Review data</param>
    /// <returns>Created review</returns>
    Task<Review> CreateAsync(CreateUserReview createReview);

    /// <summary>
    /// Get single review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    /// <returns>Review or throws if not found</returns>
    Task<Review> GetAsync(Guid id);

    /// <summary>
    /// Get reviews for a target user
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> Reviews, PagingResult Paging)> GetListAsync(Guid targetUserId, PagingQuery query);

    /// <summary>
    /// Get all user reviews with optional filtering
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Optional filter parameters</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<Review> Reviews, PagingResult Paging)> GetAllAsync(PagingQuery query, UserReviewFilter? filter = null);

    /// <summary>
    /// Get user review by author (check if author already reviewed target user)
    /// </summary>
    /// <param name="targetUserId">Target user ID</param>
    /// <param name="authorId">Review author ID</param>
    /// <returns>Review or null if not found</returns>
    Task<Review?> GetByAuthorAsync(Guid targetUserId, Guid authorId);

    /// <summary>
    /// Update user review
    /// </summary>
    /// <param name="updateReview">Update data</param>
    /// <returns>Updated review</returns>
    Task<Review> UpdateAsync(UpdateUserReview updateReview);

    /// <summary>
    /// Delete user review
    /// </summary>
    /// <param name="id">Review ID</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Check if user already has a review for the target user
    /// </summary>
    /// <param name="authorId">Review author ID</param>
    /// <param name="targetUserId">Target user ID</param>
    /// <returns>True if review already exists</returns>
    Task<bool> ExistsAsync(Guid authorId, Guid targetUserId);

    /// <summary>
    /// Check if two users have played together (were in the same game)
    /// </summary>
    /// <param name="userId1">First user ID</param>
    /// <param name="userId2">Second user ID</param>
    /// <returns>True if users have played together</returns>
    Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2);

    /// <summary>
    /// Check if review can be edited (within 24 hours of creation)
    /// </summary>
    /// <param name="review">Review to check</param>
    /// <returns>True if review can still be edited</returns>
    bool CanEdit(Review review);
}
