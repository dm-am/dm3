using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.PostReviews;

public interface IPostReviewService
{
    Task<PostReview> CreateAsync(CreatePostReview createReview);

    /// <remarks>
    /// Read through the post, whose room decides who may open it; a review of another post is not found.
    /// </remarks>
    Task<PostReview> GetAsync(Guid postId, Guid reviewId);

    Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetListAsync(Guid postId, PagingQuery query);

    Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetAllAsync(PagingQuery query, PostReviewFilter? filter = null);

    Task<PostReview> UpdateAsync(UpdatePostReview updateReview);

    Task DeleteAsync(Guid id);

    /// <summary>
    /// Exactly the posts <see cref="CreateAsync" /> takes a review of, the sign and the text aside.
    /// </summary>
    /// <remarks>
    /// The probation is not asked: it decides which signs the form offers, not whether there is one.
    /// </remarks>
    /// <param name="posts">The posts, as the current user read them</param>
    Task<IReadOnlySet<Guid>> ReviewableAsync(IReadOnlyCollection<ReviewCandidate> posts);

    /// <summary>
    /// The author's edit window is still open
    /// </summary>
    bool CanEdit(PostReview review);
}
