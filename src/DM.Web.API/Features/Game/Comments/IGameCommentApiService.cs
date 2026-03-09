using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using DiscussionResponse = DM.Web.API.Shared.Dto.DiscussionResponse;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Game.Comments;

/// <summary>
/// API service for game comments
/// </summary>
public interface IGameCommentApiService
{
    /// <summary>
    /// Get game discussion with permission flags
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>Discussion response with comments and metadata</returns>
    Task<DiscussionResponse> GetDiscussion(Guid gameId, PagingQuery query);

    /// <summary>
    /// Get game comments
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Paging query</param>
    /// <returns>Comments list with paging</returns>
    Task<ListEnvelope<Comment>> Get(Guid gameId, PagingQuery query);

    /// <summary>
    /// Create new comment
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="request">Comment creation request</param>
    /// <returns>Envelope of created comment</returns>
    Task<Envelope<Comment>> Create(Guid gameId, CreateCommentRequest request);

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
    /// Mark all game comments as read
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    Task MarkAsRead(Guid gameId);
}
