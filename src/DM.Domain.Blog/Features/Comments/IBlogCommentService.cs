using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Comments;

/// <summary>
/// Service for managing comments on blogs
/// </summary>
public interface IBlogCommentService
{
    /// <summary>
    /// Create a comment on a blog
    /// </summary>
    Task<Comment> CreateAsync(CreateComment createComment);

    /// <summary>
    /// Get comments for a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="query">Paging query</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid blogId, PagingQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment by ID
    /// </summary>
    Task<Comment> GetAsync(Guid commentId);

    /// <summary>
    /// Update a comment
    /// </summary>
    Task<Comment> UpdateAsync(UpdateComment updateComment);

    /// <summary>
    /// Delete a comment
    /// </summary>
    Task DeleteAsync(Guid commentId);

    /// <summary>
    /// Mark all blog comments as read
    /// </summary>
    Task MarkAsReadAsync(Guid blogId);
}
