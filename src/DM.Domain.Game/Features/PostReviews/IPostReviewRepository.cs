using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.PostReviews;

/// <remarks>
/// The post author's quality rating is a stored counter that moves only in the write of the row that owes it.
/// </remarks>
public interface IPostReviewRepository
{
    Task<int> CountAsync(Guid postId);

    Task<IEnumerable<PostReview>> GetAsync(Guid postId, PagingData paging);

    Task<PostReview?> GetAsync(Guid id);

    Task<int> CountAllAsync(PostReviewFilter? filter = null);

    Task<IEnumerable<PostReview>> GetAllAsync(PagingData paging, PostReviewFilter? filter = null);

    /// <summary>
    /// Which of these posts the user has a review of, removed reviews aside
    /// </summary>
    Task<IReadOnlyCollection<Guid>> SelectReviewedPostIdsAsync(Guid authorId, IReadOnlyCollection<Guid> postIds);

    /// <summary>
    /// The write moves the post author's quality rating by the sign of the review: zero for a neutral one
    /// </summary>
    Task<PostReview> CreateAsync(CreatePostReviewEntity entity);

    /// <remarks>
    /// The write locks the row and takes the rating difference itself, so a concurrent change pays for
    /// the state it actually found.
    /// </remarks>
    Task<PostReview> UpdateAsync(UpdatePostReviewEntity entity);

    /// <summary>
    /// The room access of <paramref name="userId"/> scopes the read
    /// </summary>
    Task<PostInfo?> GetPostInfoAsync(Guid postId, Guid userId);

    /// <summary>
    /// Which of these games the user has reviewed a post in since the cutoff, removed reviews aside
    /// </summary>
    Task<IReadOnlyCollection<Guid>> SelectGamesReviewedSinceAsync(
        Guid authorId, IReadOnlyCollection<Guid> gameIds, DateTimeOffset cutoffDate);

    /// <summary>
    /// The user's post count in all games
    /// </summary>
    Task<int> GetUserPostCountAsync(Guid userId);
}
