using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.PublicationComments;

/// <summary>
/// Service for managing comments on publications
/// </summary>
public interface IPublicationCommentService
{
    /// <summary>
    /// Create a comment on a publication
    /// </summary>
    Task<Comment> CreateAsync(CreateComment createComment);

    /// <summary>
    /// Whether the current user may write a new comment under the publication:
    /// true exactly when <see cref="CreateAsync" /> takes one, the text aside.
    /// The page offers the form by this answer instead of refusing what was
    /// typed.
    /// </summary>
    /// <param name="publication">The publication, read for the current user</param>
    Task<bool> MayCreateAsync(Publications.Publication publication);

    /// <summary>
    /// Get comments for a publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid publicationId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment of a publication by ID, refused the way the publication's
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
    /// Mark all publication comments as read
    /// </summary>
    Task MarkAsReadAsync(Guid publicationId);
}
