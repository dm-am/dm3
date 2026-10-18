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
    /// Whether the current user may write a new comment in the blog's
    /// discussion: true exactly when <see cref="CreateAsync" /> takes one, the
    /// text aside. The page offers the form by this answer instead of refusing
    /// what was typed.
    /// </summary>
    /// <param name="blog">The blog, read for the current user</param>
    bool MayCreate(Blogs.Blog blog);

    /// <summary>
    /// Get comments for a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid blogId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment of a blog by ID, refused the way the blog's
    /// comment listing refuses the current user. A comment of any other kind of
    /// discussion is not found
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
