using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Comments;

/// <summary>
/// Repository for blog comments
/// </summary>
public interface IBlogCommentRepository
{
    /// <summary>
    /// Count comments of the blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="query">Query parameters for filtering</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from count</param>
    /// <param name="ct">Cancellation token</param>
    Task<int> Count(Guid blogId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get comments list of the blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="query">Query parameters for filtering and sorting</param>
    /// <param name="paging">Paging data</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Comment>> Get(Guid blogId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get single comment by its identifier
    /// </summary>
    Task<Comment?> Get(Guid commentId, CancellationToken ct = default);

    /// <summary>
    /// Create comment for blog
    /// </summary>
    /// <param name="createComment">Comment creation DTO</param>
    /// <param name="authorId">Author user identifier</param>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created comment with generated ID</returns>
    Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Update comment
    /// </summary>
    /// <param name="entity">Comment update entity</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated comment</returns>
    Task<Comment> Update(UpdateBlogCommentEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Get comment for deletion with parent entity info
    /// </summary>
    Task<BlogCommentToDelete?> GetForDelete(Guid commentId, CancellationToken ct = default);

    /// <summary>
    /// Delete comment (soft delete)
    /// </summary>
    /// <remarks>
    /// The blog's comment count and its last-comment pointer travel with the mark,
    /// and the mark is conditional on the comment still being there: the answer
    /// says whether this call is the removal that happened, so a caller that has
    /// counters or events of its own does not repeat them for a comment somebody
    /// else removed first.
    /// </remarks>
    /// <param name="entity">Comment deletion entity</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Whether this call is the one that removed the comment</returns>
    Task<bool> Delete(DeleteBlogCommentEntity entity, CancellationToken ct = default);
}
