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
    /// Which of these posts the user has a review of (removed reviews aside)
    /// </summary>
    /// <param name="authorId">Author ID</param>
    /// <param name="postIds">Post IDs</param>
    Task<IReadOnlyCollection<Guid>> SelectReviewedPostIdsAsync(Guid authorId, IReadOnlyCollection<Guid> postIds);

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
    /// <remarks>
    /// What the counter owes is not passed in: an edit and a removal are the same
    /// arithmetic over the row, and a caller can only compute it from the review
    /// it read before the write, which two concurrent changes both do. The write
    /// locks the row and takes the difference itself, so the change that arrives
    /// second pays for the state it actually found.
    /// </remarks>
    /// <param name="entity">Update data: what the review becomes</param>
    Task<PostReview> UpdateAsync(UpdatePostReviewEntity entity);

    // ═══ ELIGIBILITY ═══

    /// <summary>
    /// Get post information for review creation (author ID, game ID)
    /// </summary>
    /// <param name="postId">Post ID</param>
    /// <param name="userId">Identifier of the reader asking, whose room access scopes the read</param>
    Task<PostInfo?> GetPostInfoAsync(Guid postId, Guid userId);

    /// <summary>
    /// Which of these games the user has reviewed a post in since the cutoff
    /// (removed reviews aside)
    /// </summary>
    /// <param name="authorId">Author ID</param>
    /// <param name="gameIds">Game IDs</param>
    /// <param name="cutoffDate">Cutoff date for "recent"</param>
    Task<IReadOnlyCollection<Guid>> SelectGamesReviewedSinceAsync(
        Guid authorId, IReadOnlyCollection<Guid> gameIds, DateTimeOffset cutoffDate);

    /// <summary>
    /// Get user's total post count in games (for newbie check)
    /// </summary>
    /// <param name="userId">User ID</param>
    Task<int> GetUserPostCountAsync(Guid userId);
}
