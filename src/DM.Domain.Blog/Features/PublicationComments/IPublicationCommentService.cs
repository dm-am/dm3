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
    /// Get comments for a publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid publicationId, PublicationCommentsQuery query,
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
    /// Mark all publication comments as read
    /// </summary>
    Task MarkAsReadAsync(Guid publicationId);
}
