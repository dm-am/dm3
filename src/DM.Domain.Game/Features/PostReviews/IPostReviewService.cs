using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Service for post review operations
/// </summary>
public interface IPostReviewService
{
    /// <summary>
    /// Create new post review
    /// </summary>
    /// <param name="createReview">Review data</param>
    /// <returns>Created review</returns>
    Task<PostReview> CreateAsync(CreatePostReview createReview);

    /// <summary>
    /// Get a single review of a named post.
    /// </summary>
    /// <remarks>
    /// Read through the post: a review is only as readable as the post it is
    /// about, and a post lives in a room whose access list decides who may open
    /// it. A review belonging to some other post answers as one that is not
    /// there, so the two identifiers of the route have to agree.
    /// </remarks>
    /// <param name="postId">Post the review belongs to</param>
    /// <param name="reviewId">Review ID</param>
    /// <returns>Review, or throws if the post is out of reach or the review is not its own</returns>
    Task<PostReview> GetAsync(Guid postId, Guid reviewId);

    /// <summary>
    /// Get reviews for a specific post
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="query">Paging query</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetListAsync(Guid postId, PagingQuery query);

    /// <summary>
    /// Get all post reviews with optional filtering
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Optional filter parameters</param>
    /// <returns>Reviews and paging info</returns>
    Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetAllAsync(PagingQuery query, PostReviewFilter? filter = null);

    /// <summary>
    /// Get post review by author (check if author already reviewed post)
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="authorId">Review author ID</param>
    /// <returns>Review or null if not found</returns>
    Task<PostReview?> GetByAuthorAsync(Guid postId, Guid authorId);

    /// <summary>
    /// Update post review
    /// </summary>
    /// <param name="updateReview">Update data</param>
    /// <returns>Updated review</returns>
    Task<PostReview> UpdateAsync(UpdatePostReview updateReview);

    /// <summary>
    /// Delete post review
    /// </summary>
    /// <param name="id">Review ID</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Check if user already has a review for the post
    /// </summary>
    /// <param name="authorId">Review author ID</param>
    /// <param name="postId">Post ID</param>
    /// <returns>True if review already exists</returns>
    Task<bool> ExistsAsync(Guid authorId, Guid postId);

    /// <summary>
    /// Check if user has created a post review in the specified game within cooldown period (3 days)
    /// </summary>
    /// <param name="authorId">Review author ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>True if user has a recent post review in this game</returns>
    Task<bool> HasRecentReviewInGameAsync(Guid authorId, Guid gameId);

    /// <summary>
    /// Check if review can still be edited by its author (15-minute window)
    /// </summary>
    /// <param name="review">Review to check</param>
    /// <returns>True if the edit window is still open</returns>
    bool CanEdit(PostReview review);
}
