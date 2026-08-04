using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using DiscussionResponse = DM.Web.API.Shared.Dto.DiscussionResponse;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Blog.PublicationComments;

/// <summary>
/// API service for publication commentaries
/// </summary>
public interface IPublicationCommentApiService
{
    /// <summary>
    /// Get publication commentaries
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <returns>Envelope of commentaries list</returns>
    Task<ListEnvelope<Comment>> Get(Guid publicationId, PublicationCommentsQuery query);

    /// <summary>
    /// Create new comment
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="request">Comment creation request</param>
    /// <returns>Envelope of created comment</returns>
    Task<Envelope<Comment>> Create(Guid publicationId, CreateCommentRequest request);

    /// <summary>
    /// Get comment by identifier
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Envelope of comment</returns>
    Task<Envelope<Comment>> Get(Guid commentId);

    /// <summary>
    /// Update comment by API DTO model
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <param name="comment">Comment DTO model</param>
    /// <returns>Envelope of updated comment</returns>
    Task<Envelope<Comment>> Update(Guid commentId, Comment comment);

    /// <summary>
    /// Delete comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task Delete(Guid commentId);

    /// <summary>
    /// Mark all publication comments as read
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    Task MarkAsRead(Guid publicationId);
}
