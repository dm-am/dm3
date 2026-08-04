using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Comments;
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
    /// Get game comments
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="query">Query with filtering, sorting and paging</param>
    /// <returns>Comments list with paging</returns>
    Task<ListEnvelope<Comment>> Get(Guid gameId, GameCommentsQuery query);

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
    /// <returns>Envelope containing the comment</returns>
    Task<Envelope<Comment>> Get(Guid commentId);

    /// <summary>
    /// Update comment by API DTO model
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <param name="comment">Comment DTO model</param>
    /// <returns>Envelope containing the updated comment</returns>
    Task<Envelope<Comment>> Update(Guid commentId, Comment comment);

    /// <summary>
    /// Delete comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task Delete(Guid commentId);

    /// <summary>
    /// Mark all game comments as read
    /// </summary>
    /// <param name="gameId"></param>
    Task MarkAsRead(Guid gameId);
}
