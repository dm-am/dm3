using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Shared;

namespace DM.Web.API.Services.Blog;

/// <summary>
/// API service for publication commentaries
/// </summary>
public interface IPublicationCommentApiService
{
    /// <summary>
    /// Get publication discussion with permission flags
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>Discussion response with comments and metadata</returns>
    Task<DiscussionResponse> GetDiscussion(Guid publicationId, PagingQuery query);

    /// <summary>
    /// Get publication commentaries
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>Envelope of commentaries list</returns>
    Task<ListEnvelope<Comment>> Get(Guid publicationId, PagingQuery query);

    /// <summary>
    /// Create new comment
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <param name="comment">Comment model</param>
    /// <returns>Envelope of created comment</returns>
    Task<Envelope<Comment>> Create(Guid publicationId, Comment comment);

    /// <summary>
    /// Get comment by identifier
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task<Envelope<Comment>> Get(Guid commentId);

    /// <summary>
    /// Update comment by API DTO model
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <param name="comment">Comment DTO model</param>
    /// <returns></returns>
    Task<Envelope<Comment>> Update(Guid commentId, Comment comment);

    /// <summary>
    /// Delete comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task Delete(Guid commentId);

    /// <summary>
    /// Mark all publication comments as read
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns></returns>
    Task MarkAsRead(Guid publicationId);
}
