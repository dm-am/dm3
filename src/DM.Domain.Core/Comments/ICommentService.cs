using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Comments;

/// <summary>
/// Generic service for comment management across domains (Game, Blog, Forum)
/// </summary>
public interface ICommentService
{
    /// <summary>
    /// Get comments for an entity
    /// </summary>
    Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetComments(
        CommentEntityType entityType,
        Guid entityId,
        PagingQuery paging,
        CancellationToken ct = default);

    /// <summary>
    /// Get single comment by ID
    /// </summary>
    Task<Comment?> GetComment(Guid commentId, CancellationToken ct = default);

    /// <summary>
    /// Create a comment
    /// </summary>
    Task<Comment> CreateComment(
        CommentEntityType entityType,
        Guid entityId,
        CreateComment createComment,
        CancellationToken ct = default);

    /// <summary>
    /// Update a comment
    /// </summary>
    Task<Comment> UpdateComment(Guid commentId, UpdateComment updateComment, CancellationToken ct = default);

    /// <summary>
    /// Delete a comment
    /// </summary>
    Task DeleteComment(Guid commentId, CancellationToken ct = default);
}
