using System;
using DM.Domain.Core.Comments;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Blog.Comments;

/// <summary>
/// API service for blog discussion comments
/// </summary>
public interface IBlogCommentApiService
{
    /// <summary>
    /// Get blog comments
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <returns>Comments list with paging</returns>
    Task<ListEnvelope<Comment>> Get(Guid blogId, CommentsQuery query);

    /// <summary>
    /// Create new comment on blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="request">Comment creation request</param>
    /// <returns>Envelope of created comment</returns>
    Task<Envelope<Comment>> Create(Guid blogId, CreateCommentRequest request);

    /// <summary>
    /// Get comment by identifier
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Envelope of comment</returns>
    Task<Envelope<Comment>> Get(Guid commentId);

    /// <summary>
    /// Get the markup of a quotation of a comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Envelope containing the quotation source</returns>
    Task<Envelope<QuoteSource>> GetQuote(Guid commentId);

    /// <summary>
    /// Update comment by API DTO model
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <param name="request">Updated comment text</param>
    /// <returns>Envelope of updated comment</returns>
    Task<Envelope<Comment>> Update(Guid commentId, UpdateCommentRequest request);

    /// <summary>
    /// Delete blog comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task Delete(Guid commentId);

    /// <summary>
    /// Mark all blog comments as read
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    Task MarkAsRead(Guid blogId);
}
