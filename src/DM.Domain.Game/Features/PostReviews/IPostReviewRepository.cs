using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Repository for post review operations
/// </summary>
/// <remarks>
/// The post author's quality rating is a stored counter and not a sum over the
/// reviews, so nothing recomputes it: it travels with the row that owes it, in
/// the same write, and is never moved by a call of its own.
/// </remarks>
public interface IPostReviewRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Get total count of reviews for a post
    /// </summary>
    /// <param name="postId">Post ID</param>
    Task<int> CountAsync(Guid postId);

    /// <summary>
    /// Get reviews for a post
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="paging">Paging parameters</param>
    Task<IEnumerable<PostReview>> GetAsync(Guid postId, PagingData paging);

    /// <summary>
    /// Get single post review by ID
    /// </summary>
    /// <param name="id">Review ID</param>
    Task<PostReview?> GetAsync(Guid id);

    /// <summary>
    /// Get post review by author
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="authorId">Author ID</param>
    Task<PostReview?> GetByAuthorAsync(Guid postId, Guid authorId);

    /// <summary>
    /// Get count of all post reviews with optional filter
    /// </summary>
    /// <param name="filter">Optional filter parameters</param>
    Task<int> CountAllAsync(PostReviewFilter? filter = null);

    /// <summary>
    /// Get all post reviews with optional filter
    /// </summary>
    /// <param name="paging">Paging parameters</param>
    /// <param name="filter">Optional filter parameters</param>
    Task<IEnumerable<PostReview>> GetAllAsync(PagingData paging, PostReviewFilter? filter = null);

    // ═══ WRITE ═══

    /// <summary>
    /// Check if user already has a review for the post
    /// </summary>
    /// <param name="authorId">Author ID</param>
    /// <param name="postId">Post ID</param>
    Task<bool> ExistsAsync(Guid authorId, Guid postId);

    /// <summary>
    /// Create new post review, moving the post author's quality rating with it
    /// </summary>
    /// <param name="entity">Review data</param>
    /// <param name="qualityRatingDelta">
    /// What the review is worth to the post author's quality rating: zero for a
    /// neutral review, the sign otherwise
    /// </param>
    Task<PostReview> CreateAsync(CreatePostReviewEntity entity, int qualityRatingDelta);

    /// <summary>
    /// Update post review, moving the post author's quality rating with it
    /// </summary>
    /// <param name="entity">Update data</param>
    /// <param name="qualityRatingDelta">
    /// What the change is worth to the post author's quality rating: the
    /// difference between the new sign and the old one on an edit, the sign
    /// taken back on a removal, zero when neither moves
    /// </param>
    Task<PostReview> UpdateAsync(UpdatePostReviewEntity entity, int qualityRatingDelta);

    // ═══ ELIGIBILITY ═══

    /// <summary>
    /// Get post information for review creation (author ID, game ID)
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="userId">Identifier of the reader asking, whose room access scopes the read</param>
    Task<PostInfo?> GetPostInfoAsync(Guid postId, Guid userId);

    /// <summary>
    /// Check if user has a recent post review in the specified game
    /// </summary>
    /// <param name="authorId">Author ID</param>
    /// <param name="gameId">Game ID</param>
    /// <param name="cutoffDate">Cutoff date for "recent"</param>
    Task<bool> HasRecentReviewInGameAsync(Guid authorId, Guid gameId, DateTimeOffset cutoffDate);

    /// <summary>
    /// Get user's total post count in games (for newbie check)
    /// </summary>
    /// <param name="userId">User ID</param>
    Task<int> GetUserPostCountAsync(Guid userId);
}
