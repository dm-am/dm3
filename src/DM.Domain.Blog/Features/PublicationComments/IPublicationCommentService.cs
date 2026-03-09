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
    Task<Comment> Create(CreateComment createComment);

    /// <summary>
    /// Get comments for a publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="query">Paging query</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> Get(Guid publicationId, PagingQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment by ID
    /// </summary>
    Task<Comment> Get(Guid commentId);

    /// <summary>
    /// Update a comment
    /// </summary>
    Task<Comment> Update(UpdateComment updateComment);

    /// <summary>
    /// Delete a comment
    /// </summary>
    Task Delete(Guid commentId);

    /// <summary>
    /// Mark all publication comments as read
    /// </summary>
    Task MarkAsRead(Guid publicationId);
}
