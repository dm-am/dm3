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
    /// <param name="newCommentCount">New total comment count for blog</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created comment with generated ID</returns>
    Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid blogId, int newCommentCount, CancellationToken ct = default);

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
    /// The newest live comment of the blog other than <paramref name="exceptCommentId" />:
    /// the successor of the comment being deleted.
    /// </summary>
    /// <remarks>
    /// Named by exclusion rather than by position — see the note on the forum's
    /// counterpart. Two comments can share a timestamp, and then "the second one down the
    /// list" can be the row that is leaving, which would leave the blog pointing at a
    /// soft-deleted comment.
    /// </remarks>
    Task<Guid?> GetNewestCommentIdExcept(Guid blogId, Guid exceptCommentId, CancellationToken ct = default);

    /// <summary>
    /// Delete comment (soft delete)
    /// </summary>
    /// <param name="entity">Comment deletion entity</param>
    /// <param name="ct">Cancellation token</param>
    Task Delete(DeleteBlogCommentEntity entity, CancellationToken ct = default);
}
